using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Tenancy.Contracts;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.Persistence.Abstractions;

namespace PaymentReconciliation.Modules.Tenancy.Application;

public sealed class TenantDirectory(IAppDbContext dbContext) : ITenantDirectory
{
    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(
        IReadOnlyCollection<Guid> tenantIds,
        CancellationToken cancellationToken)
        => await dbContext.Set<Tenant>()
            .AsNoTracking()
            .Where(tenant => tenantIds.Contains(tenant.Id))
            .ToDictionaryAsync(tenant => tenant.Id, tenant => tenant.Name, cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> FindIdsByNameAsync(
        string search,
        CancellationToken cancellationToken)
        => await dbContext.Set<Tenant>()
            .AsNoTracking()
            .Where(tenant => tenant.Name.Contains(search))
            .Select(tenant => tenant.Id)
            .ToListAsync(cancellationToken);
}
