using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentReconciliation.Modules.Webhooks.Application;
using PaymentReconciliation.Modules.Webhooks.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;

namespace PaymentReconciliation.Modules.Webhooks.Infrastructure;

public sealed class WebhookRecoveryWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<WebhookIngestionOptions> options,
    ILogger<WebhookRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

        try
        {
            do
            {
                await RecoverStuckEventsAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Planned shutdown.
        }
    }

    private async Task RecoverStuckEventsAsync(CancellationToken cancellationToken)
    {
        try
        {
            List<(Guid Id, Guid TenantId)> stuck;

            using (var lookupScope = scopeFactory.CreateScope())
            {
                var dbContext = lookupScope.ServiceProvider.GetRequiredService<IAppDbContext>();
                var cutoff = DateTimeOffset.UtcNow.AddMinutes(-Math.Max(1, options.Value.StuckMinutes));
                var rows = await dbContext.Set<WebhookEvent>()
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(webhookEvent =>
                        (webhookEvent.Status == WebhookStatus.Received
                         || webhookEvent.Status == WebhookStatus.Processing)
                        && webhookEvent.ReceivedAt < cutoff)
                    .Select(webhookEvent => new { webhookEvent.Id, webhookEvent.TenantId })
                    .ToListAsync(cancellationToken);

                stuck = rows
                    .Select(item => (item.Id, item.TenantId))
                    .ToList();
            }

            foreach (var (id, tenantId) in stuck)
            {
                using var scope = scopeFactory.CreateScope();
                scope.ServiceProvider.GetRequiredService<ITenantContext>()
                    .Set(new TenantContextSnapshot(tenantId, string.Empty, null, null, false));
                var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var webhookEvent = await dbContext.Set<WebhookEvent>()
                    .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

                if (webhookEvent is null)
                {
                    continue;
                }

                if (webhookEvent.AttemptCount >= Math.Max(1, options.Value.MaxAttempts))
                {
                    webhookEvent.MarkFailed("processing_interrupted");
                    await unitOfWork.DispatchDomainEventsAsync(cancellationToken);
                }
                else
                {
                    webhookEvent.ScheduleRetry("processing_interrupted", DateTimeOffset.UtcNow);
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            if (stuck.Count > 0)
            {
                logger.LogWarning("Recovered {Count} interrupted webhook event(s).", stuck.Count);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Planned shutdown.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Webhook recovery sweep failed.");
        }
    }
}
