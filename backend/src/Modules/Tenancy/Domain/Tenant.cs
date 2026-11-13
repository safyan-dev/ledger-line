using PaymentReconciliation.Modules.Tenancy.Contracts.Events;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Domain;

namespace PaymentReconciliation.Modules.Tenancy.Domain;

public enum TenantStatus
{
    Active,
    Suspended,
    Canceled
}

public enum TenantRole
{
    Owner,
    Admin,
    Finance
}

public static class TenantRoleRules
{
    public static bool CanManageTenant(TenantRole role)
        => role is TenantRole.Owner or TenantRole.Admin;

    public static bool CanAssignRole(TenantRole actorRole, TenantRole targetRole)
        => actorRole == TenantRole.Owner
           || (actorRole == TenantRole.Admin && targetRole != TenantRole.Owner);

    public static bool TryParse(string? value, out TenantRole role)
        => Enum.TryParse(value, ignoreCase: true, out role);
}

public sealed class Tenant : AggregateRoot
{
    private Tenant()
    {
    }

    private Tenant(Guid id, string name, string slug, DateTimeOffset createdAt)
        : base(id)
    {
        Name = name;
        Slug = slug;
        Status = TenantStatus.Active;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public TenantStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Tenant Create(string name, string slug, DateTimeOffset? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Tenant name is required.");
        }

        var normalizedSlug = NormalizeSlug(slug);
        if (normalizedSlug.Length is < 2 or > 40)
        {
            throw new DomainException("Tenant slug must be between 2 and 40 characters.");
        }

        var now = createdAt ?? DateTimeOffset.UtcNow;
        return new Tenant(Guid.NewGuid(), name.Trim(), normalizedSlug, now);
    }

    public void Rename(string name, AuditActor actor)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Tenant name is required.");
        }

        var trimmed = name.Trim();
        if (trimmed.Length > 160)
        {
            throw new DomainException("Tenant name must be at most 160 characters.");
        }

        if (string.Equals(Name, trimmed, StringComparison.Ordinal))
        {
            return;
        }

        Name = trimmed;
        UpdatedAt = DateTimeOffset.UtcNow;
        Raise(new TenantRenamed(Id, Name, actor));
    }

    public void Suspend()
    {
        Status = TenantStatus.Suspended;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate()
    {
        Status = TenantStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static string NormalizeSlug(string slug)
        => slug.Trim().ToLowerInvariant();

    public static bool IsValidSlug(string slug)
    {
        var normalized = NormalizeSlug(slug);
        return normalized.Length is >= 2 and <= 40
               && normalized.All(character => char.IsLetterOrDigit(character) || character == '-')
               && char.IsLetterOrDigit(normalized[0])
               && char.IsLetterOrDigit(normalized[^1]);
    }
}

public sealed class TenantUser : AggregateRoot
{
    private TenantUser()
    {
    }

    private TenantUser(Guid id, Guid tenantId, Guid userId, TenantRole role, DateTimeOffset createdAt)
        : base(id)
    {
        TenantId = tenantId;
        UserId = userId;
        Role = role;
        CreatedAt = createdAt;
    }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public TenantRole Role { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static TenantUser Create(Guid tenantId, Guid userId, TenantRole role)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            throw new DomainException("Tenant membership requires a tenant and user.");
        }

        return new TenantUser(Guid.NewGuid(), tenantId, userId, role, DateTimeOffset.UtcNow);
    }

    public static TenantUser Add(Guid tenantId, Guid userId, TenantRole role, AuditActor actor)
    {
        var membership = Create(tenantId, userId, role);
        membership.Raise(new TenantMemberAdded(tenantId, userId, role.ToString(), actor));
        return membership;
    }

    public void ChangeRole(TenantRole role, AuditActor actor)
    {
        if (Role == role)
        {
            return;
        }

        var previousRole = Role;
        Role = role;
        Raise(new TenantMemberRoleChanged(TenantId, UserId, previousRole.ToString(), role.ToString(), actor));
    }

    public void Remove(AuditActor actor) 
        => Raise(new TenantMemberRemoved(TenantId, UserId, Role.ToString(), actor));
}
