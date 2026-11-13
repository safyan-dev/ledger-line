using Microsoft.AspNetCore.Authorization;

namespace PaymentReconciliation.Modules.Tenancy.Infrastructure;

public static class TenancyAuthorization
{
    public static AuthorizationPolicy TenantScoped { get; } = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .AddRequirements(new TenantMemberRequirement())
        .Build();
}
