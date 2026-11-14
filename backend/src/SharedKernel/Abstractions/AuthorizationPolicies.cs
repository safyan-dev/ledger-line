namespace PaymentReconciliation.SharedKernel.Abstractions;

public static class AuthorizationPolicies
{
    public const string AuthenticatedUser = "AuthenticatedUser";

    public const string PlatformAdmin = "PlatformAdmin";

    public const string TenantMember = "TenantMember";

    public const string TenantOwner = "TenantOwner";

    public const string TenantOwnerOrAdmin = "TenantOwnerOrAdmin";
}
