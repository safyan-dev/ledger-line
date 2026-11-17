using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Tenancy.Infrastructure;

public interface ITenancyResolver
{
    Task<Result<TenantContextSnapshot?>> ResolveAsync(
        string? tenantSlug,
        Guid? userId,
        bool allowAnonymousTenant,
        bool isPlatformAdmin,
        bool allowInactiveSubscription,
        CancellationToken cancellationToken = default);
}

public sealed class TenantResolutionService(IAppDbContext dbContext) : ITenancyResolver
{
    public async Task<Result<TenantContextSnapshot?>> ResolveAsync(
        string? tenantSlug,
        Guid? userId,
        bool allowAnonymousTenant,
        bool isPlatformAdmin,
        bool allowInactiveSubscription,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantSlug))
        {
            if (allowAnonymousTenant)
            {
                return Result.Failure<TenantContextSnapshot?>(Error.NotFound(
                    "tenant.not_resolved",
                    "A tenant subdomain is required for webhook ingress."));
            }

            return userId is null
                ? Result.Success<TenantContextSnapshot?>(null)
                : Result.Failure<TenantContextSnapshot?>(Error.Forbidden(
                    "tenant.required",
                    "A tenant subdomain is required."));
        }

        var slug = Tenant.NormalizeSlug(tenantSlug);
        var tenant = await dbContext.Set<Tenant>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Slug == slug, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<TenantContextSnapshot?>(Error.NotFound(
                "tenant.not_found",
                "Tenant was not found."));
        }

        if (tenant.Status != TenantStatus.Active)
        {
            return Result.Failure<TenantContextSnapshot?>(Error.Forbidden(
                "tenant.inactive",
                "Tenant is not active."));
        }

        if (userId is null)
        {
            if (!allowInactiveSubscription
                && !await HasOperationalSubscriptionAsync(tenant.Id, cancellationToken))
            {
                return InactiveSubscription();
            }

            return Result.Success<TenantContextSnapshot?>(allowAnonymousTenant
                ? new TenantContextSnapshot(tenant.Id, tenant.Slug, null, null, false)
                : null);
        }

        var membership = await dbContext.Set<TenantUser>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.TenantId == tenant.Id && candidate.UserId == userId,
                cancellationToken);

        if (membership is null)
        {
            return Result.Failure<TenantContextSnapshot?>(Error.Forbidden(
                "tenant.membership_required",
                "You are not a member of this tenant."));
        }

        if (!allowInactiveSubscription
            && !await HasOperationalSubscriptionAsync(tenant.Id, cancellationToken))
        {
            return InactiveSubscription();
        }

        return Result.Success<TenantContextSnapshot?>(new TenantContextSnapshot(
            tenant.Id,
            tenant.Slug,
            userId,
            membership.Role.ToString(),
            isPlatformAdmin));
    }

    private async Task<bool> HasOperationalSubscriptionAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var subscription = await dbContext.Set<Subscription>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.TenantId == tenantId, cancellationToken);

        return subscription?.IsActive == true;
    }

    private static Result<TenantContextSnapshot?> InactiveSubscription()
        => Result.Failure<TenantContextSnapshot?>(Error.Forbidden(
            "subscription.inactive",
            "The workspace subscription is not active. Reactivate the subscription to continue."));
}

public sealed class TenantMemberRequirement : IAuthorizationRequirement;

public sealed class TenantMemberAuthorizationHandler(ITenantContext tenantContext)
    : AuthorizationHandler<TenantMemberRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantMemberRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true && tenantContext.IsResolved && tenantContext.UserId is not null)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public sealed class TenantOwnerRequirement : IAuthorizationRequirement;

public sealed class TenantOwnerAuthorizationHandler(ITenantContext tenantContext)
    : AuthorizationHandler<TenantOwnerRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantOwnerRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && tenantContext.IsResolved
            && TenantRoleRules.TryParse(tenantContext.Role, out var role)
            && role == TenantRole.Owner)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public sealed class TenantOwnerOrAdminRequirement : IAuthorizationRequirement;

public sealed class TenantOwnerOrAdminAuthorizationHandler(ITenantContext tenantContext)
    : AuthorizationHandler<TenantOwnerOrAdminRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantOwnerOrAdminRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && tenantContext.IsResolved
            && TenantRoleRules.TryParse(tenantContext.Role, out var role)
            && role is TenantRole.Owner or TenantRole.TenantAdmin)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
