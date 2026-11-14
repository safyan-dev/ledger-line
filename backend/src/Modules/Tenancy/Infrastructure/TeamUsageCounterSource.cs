using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Tenancy.Contracts;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.Persistence.Abstractions;

namespace PaymentReconciliation.Modules.Tenancy.Infrastructure;

public sealed class TeamUsageCounterSource(IAppDbContext dbContext) : IUsageCounterSource
{
    public UsageMetric Metric => UsageMetric.TeamMembers;

    public Task<int> CountAsync(
        Guid tenantId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken)
        => dbContext.Set<TenantUser>()
            .CountAsync(membership => membership.TenantId == tenantId, cancellationToken);
}
