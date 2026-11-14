using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Orders.Domain;
using PaymentReconciliation.Modules.Tenancy.Contracts;
using PaymentReconciliation.Persistence.Abstractions;

namespace PaymentReconciliation.Modules.Orders.Infrastructure;

public sealed class OrderUsageCounterSource(IAppDbContext dbContext) : IUsageCounterSource
{
    public UsageMetric Metric => UsageMetric.OrdersPerMonth;

    public Task<int> CountAsync(
        Guid tenantId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken)
        => dbContext.Set<Order>()
            .IgnoreQueryFilters()
            .CountAsync(
                order => order.TenantId == tenantId
                         && order.CreatedAt >= periodStart
                         && order.CreatedAt < periodEnd,
                cancellationToken);
}
