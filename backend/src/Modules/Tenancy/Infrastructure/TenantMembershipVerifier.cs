using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Identity.Contracts;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.Persistence.Abstractions;

namespace PaymentReconciliation.Modules.Tenancy.Infrastructure;

public sealed class TenantMembershipVerifier(IAppDbContext dbContext) : ITenantMembershipVerifier
{
    public async Task<TenantLoginInfo?> FindForLoginAsync(
        Guid userId,
        string tenantSlug,
        CancellationToken cancellationToken = default)
    {
        if (!Tenant.IsValidSlug(tenantSlug))
        {
            return null;
        }

        var slug = Tenant.NormalizeSlug(tenantSlug);
        var tenant = await dbContext.Set<Tenant>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Slug == slug, cancellationToken);

        if (tenant is null || tenant.Status != TenantStatus.Active)
        {
            return null;
        }

        var membership = await dbContext.Set<TenantUser>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.TenantId == tenant.Id && candidate.UserId == userId,
                cancellationToken);

        return membership is null
            ? null
            : new TenantLoginInfo(tenant.Id, tenant.Name, tenant.Slug, membership.Role.ToString());
    }
}
