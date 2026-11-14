using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Tenancy.Contracts;

public enum UsageMetric
{
    OrdersPerMonth,
    PaymentsPerMonth,
    TeamMembers
}

public interface IUsageLimitEnforcer
{
    Task<Result> EnsureWithinLimitAsync(
        UsageMetric metric,
        CancellationToken cancellationToken = default);
}

public interface IUsageCounterSource
{
    UsageMetric Metric { get; }

    Task<int> CountAsync(
        Guid tenantId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken);
}
