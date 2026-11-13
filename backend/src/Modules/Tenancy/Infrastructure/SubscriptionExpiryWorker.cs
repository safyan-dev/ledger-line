using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.Persistence.Abstractions;

namespace PaymentReconciliation.Modules.Tenancy.Infrastructure;

public sealed class SubscriptionExpiryService(
    IAppDbContext dbContext,
    IUnitOfWork unitOfWork,
    ITenantBoundaryBypass tenantBoundaryBypass)
{
    public async Task<int> ExpireDueAsync(CancellationToken cancellationToken = default)
    {
        using var _ = tenantBoundaryBypass.Begin();

        var now = DateTimeOffset.UtcNow;

        var due = await dbContext.Set<Subscription>()
            .IgnoreQueryFilters()
            .Where(subscription =>
                (subscription.Status == SubscriptionStatus.Active
                 || subscription.Status == SubscriptionStatus.Trialing)
                && subscription.CurrentPeriodEnd != null
                && subscription.CurrentPeriodEnd <= now)
            .ToListAsync(cancellationToken);

        if (due.Count == 0)
        {
            return 0;
        }

        foreach (var subscription in due)
        {
            if (!subscription.Expire(now))
            {
                continue;
            }

            dbContext.Set<SubscriptionHistory>().Add(SubscriptionHistory.Create(
                subscription.TenantId,
                subscription.Id,
                subscription.PlanId,
                "subscription.expired",
                subscription.Status,
                note: "Subscription expired. Tenant data was retained.",
                occurredAt: now));
        }

        var dispatch = await unitOfWork.DispatchDomainEventsAsync(cancellationToken);
        if (dispatch.IsFailure)
        {
            throw new InvalidOperationException(dispatch.Error!.Message);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return due.Count;
    }
}

public sealed class SubscriptionExpiryWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<SubscriptionExpiryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<SubscriptionExpiryService>();
                await service.ExpireDueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Subscription expiry sweep failed.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
