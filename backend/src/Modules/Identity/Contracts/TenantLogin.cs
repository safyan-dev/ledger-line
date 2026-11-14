namespace PaymentReconciliation.Modules.Identity.Contracts;

public sealed record TenantLoginInfo(
    Guid TenantId,
    string TenantName,
    string TenantSlug,
    string Role);

public interface ITenantMembershipVerifier
{
    Task<TenantLoginInfo?> FindForLoginAsync(
        Guid userId,
        string tenantSlug,
        CancellationToken cancellationToken = default);
}
