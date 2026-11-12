using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentReconciliation.Modules.Webhooks.Application;
using PaymentReconciliation.Modules.Webhooks.Domain;
using PaymentReconciliation.Persistence.Abstractions;

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
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var cutoff = DateTimeOffset.UtcNow.AddMinutes(-Math.Max(1, options.Value.StuckMinutes));

            var stuckEvents = await dbContext.Set<WebhookEvent>()
                .Where(webhookEvent =>
                    (webhookEvent.Status == WebhookStatus.Received || webhookEvent.Status == WebhookStatus.Processing)
                    && webhookEvent.ReceivedAt < cutoff)
                .ToListAsync(cancellationToken);

            if (stuckEvents.Count == 0)
            {
                return;
            }

            foreach (var webhookEvent in stuckEvents)
            {
                webhookEvent.MarkFailed("processing_interrupted");
            }

            await unitOfWork.DispatchDomainEventsAsync(cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                "Marked {Count} interrupted webhook event(s) as failed.",
                stuckEvents.Count);
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
