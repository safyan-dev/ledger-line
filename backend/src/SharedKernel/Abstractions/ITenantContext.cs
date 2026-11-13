namespace PaymentReconciliation.SharedKernel.Abstractions;

public sealed record TenantContextSnapshot(
    Guid TenantId,
    string TenantSlug,
    Guid? UserId,
    string? Role,
    bool IsPlatformAdmin);

public interface ITenantContext
{
    Guid? TenantId { get; }

    string? TenantSlug { get; }

    Guid? UserId { get; }

    string? Role { get; }

    bool IsPlatformAdmin { get; }

    bool IsResolved { get; }

    void Set(TenantContextSnapshot? snapshot);
}

public sealed class TenantContext : ITenantContext
{
    private TenantContextSnapshot? _snapshot;

    public Guid? TenantId => _snapshot?.TenantId;

    public string? TenantSlug => _snapshot?.TenantSlug;

    public Guid? UserId => _snapshot?.UserId;

    public string? Role => _snapshot?.Role;

    public bool IsPlatformAdmin => _snapshot?.IsPlatformAdmin ?? false;

    public bool IsResolved => _snapshot is not null;

    public void Set(TenantContextSnapshot? snapshot) => _snapshot = snapshot;
}

public interface ITenantOwned
{
    Guid TenantId { get; }
}
