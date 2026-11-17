using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Tenancy.Contracts;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Tenancy.Application;

public interface IUsageService : IUsageLimitEnforcer
{
    Task<UsageSummaryDto?> GetUsageAsync(CancellationToken cancellationToken = default);

    Task<bool> IsFeatureEnabledAsync(string featureKey, CancellationToken cancellationToken = default);
}

public sealed class UsageService(
    IAppDbContext dbContext,
    ITenantContext tenantContext,
    IEntitlementService entitlements,
    IEnumerable<IUsageCounterSource> counterSources,
    IUnitOfWork unitOfWork,
    IClock clock) : IUsageService
{
    private static readonly (UsageMetric Metric, string LimitKey, string DefaultLabel)[] Definitions =
    [
        (UsageMetric.OrdersPerMonth, "orders_per_month", "Orders"),
        (UsageMetric.PaymentsPerMonth, "payments_per_month", "Payments"),
        (UsageMetric.TeamMembers, "team_members", "Team members")
    ];

    public async Task<UsageSummaryDto?> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return null;
        }

        var entitlement = await entitlements.GetEntitlementsAsync(cancellationToken);
        if (entitlement is null)
        {
            return null;
        }

        var periodStart = MonthStart(clock.UtcNow);
        var periodEnd = periodStart.AddMonths(1);
        var sources = counterSources.ToDictionary(source => source.Metric);
        var metrics = new List<UsageMetricDto>(Definitions.Length);

        foreach (var definition in Definitions)
        {
            var limit = entitlement.Limits.FirstOrDefault(candidate =>
                string.Equals(candidate.Key, definition.LimitKey, StringComparison.OrdinalIgnoreCase));

            var used = sources.TryGetValue(definition.Metric, out var source)
                ? await source.CountAsync(tenantId, periodStart, periodEnd, cancellationToken)
                : 0;

            metrics.Add(new UsageMetricDto(
                definition.Metric.ToString(),
                limit?.Label ?? definition.DefaultLabel,
                used,
                limit?.Value,
                limit?.Value is { } limitValue ? limitValue - used : null,
                definition.Metric == UsageMetric.TeamMembers ? "current" : periodStart.ToString("yyyy-MM")));
        }

        return new UsageSummaryDto(
            entitlement.PlanCode,
            entitlement.PlanName,
            entitlement.Status,
            periodStart,
            periodEnd,
            metrics,
            entitlement.Features);
    }

    public async Task<Result> EnsureWithinLimitAsync(
        UsageMetric metric,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure(Error.Forbidden("tenant.required", "A tenant context is required."));
        }

        if (!unitOfWork.HasActiveTransaction)
        {
            return Result.Failure(Error.Unexpected(
                "usage.transaction_required",
                "Plan limits can only be enforced inside a transaction."));
        }

        var subscription = await dbContext.Set<Subscription>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.TenantId == tenantId, cancellationToken);
        if (subscription is null || !subscription.IsActive)
        {
            return Result.Failure(Error.Forbidden(
                "subscription.inactive",
                "The workspace subscription is not active. Reactivate the subscription to continue."));
        }

        var entitlement = await entitlements.GetEntitlementsAsync(cancellationToken);
        if (entitlement is null)
        {
            return Result.Success();
        }

        var definition = Definitions.First(candidate => candidate.Metric == metric);
        var limit = entitlement.Limits.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, definition.LimitKey, StringComparison.OrdinalIgnoreCase));

        if (limit?.Value is not { } limitValue)
        {
            return Result.Success();
        }

        var source = counterSources.FirstOrDefault(candidate => candidate.Metric == metric);
        if (source is null)
        {
            return Result.Success();
        }

        var lockKey = $"{tenantId:N}:{metric}";

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);

        var periodStart = MonthStart(clock.UtcNow);
        var periodEnd = periodStart.AddMonths(1);
        var used = await source.CountAsync(tenantId, periodStart, periodEnd, cancellationToken);

        if (used >= limitValue)
        {
            return Result.Failure(Error.Forbidden(
                "plan.limit_reached",
                $"The {limit.Label} limit of {limitValue} for the {entitlement.PlanName} plan has been reached."));
        }

        return Result.Success();
    }

    public Task<bool> IsFeatureEnabledAsync(
        string featureKey,
        CancellationToken cancellationToken = default)
        => entitlements.IsFeatureEnabledAsync(featureKey, cancellationToken);

    private static DateTimeOffset MonthStart(DateTimeOffset now)
        => new(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
}
