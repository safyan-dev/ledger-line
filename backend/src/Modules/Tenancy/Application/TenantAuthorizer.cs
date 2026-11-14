using PaymentReconciliation.Modules.Tenancy.Contracts;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Tenancy.Application;

public sealed class TenantAuthorizer(ITenantContext tenantContext) : ITenantAuthorizer
{
    public bool IsGranted(TenantPermission permission)
    {
        if (!tenantContext.IsResolved)
        {
            return false;
        }

        return TenantRoleRules.TryParse(tenantContext.Role, out var role)
               && TenantRolePermissions.IsGranted(role, permission);
    }

    public Result Require(TenantPermission permission)
    {
        if (!tenantContext.IsResolved)
        {
            return Result.Failure(Error.Forbidden(
                "tenant.required",
                "A tenant context is required."));
        }

        if (!TenantRoleRules.TryParse(tenantContext.Role, out var role))
        {
            return Result.Failure(Error.Forbidden(
                "tenant.permission_denied",
                "Your role is not valid for this tenant."));
        }

        if (!TenantRolePermissions.IsGranted(role, permission))
        {
            return Result.Failure(Error.Forbidden(
                "tenant.permission_denied",
                $"Your role does not allow this action ({permission})."));
        }

        return Result.Success();
    }
}
