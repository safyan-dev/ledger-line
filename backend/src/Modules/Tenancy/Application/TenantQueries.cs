using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Tenancy.Application;

public sealed class TenantQueries(IAppDbContext dbContext, ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<MembershipDto>>> GetMembershipsAsync(
        CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<IReadOnlyList<MembershipDto>>(Error.Unauthorized(
                "auth.required",
                "Authentication is required."));
        }

        var memberships = await (
                from membership in dbContext.Set<TenantUser>().AsNoTracking()
                join tenant in dbContext.Set<Tenant>().AsNoTracking() on membership.TenantId equals tenant.Id
                where membership.UserId == userId
                orderby tenant.Name
                select new MembershipDto(tenant.Id, tenant.Name, tenant.Slug, membership.Role.ToString()))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<MembershipDto>>(memberships);
    }
}
