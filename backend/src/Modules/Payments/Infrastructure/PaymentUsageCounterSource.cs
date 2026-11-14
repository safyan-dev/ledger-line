using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Payments.Domain;
using PaymentReconciliation.Modules.Tenancy.Contracts;
using PaymentReconciliation.Persistence.Abstractions;

namespace PaymentReconciliation.Modules.Payments.Infrastructure;

public sealed class PaymentUsageCounterSource(IAppDbContext dbContext) : IUsageCounterSource
{
    public UsageMetric Metric => UsageMetric.PaymentsPerMonth;

    public Task<int> CountAsync(
        Guid tenantId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken)
        => dbContext.Set<Payment>()
            .IgnoreQueryFilters()
            .CountAsync(
                payment => payment.TenantId == tenantId
                           && payment.CreatedAt >= periodStart
                           && payment.CreatedAt < periodEnd,
                cancellationToken);
}
