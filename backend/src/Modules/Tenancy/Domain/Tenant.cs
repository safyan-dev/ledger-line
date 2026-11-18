using System.Net.Mail;
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
    TenantAdmin,
    Finance
}

public static class TenantRoleRules
{
    public static bool CanManageTenant(TenantRole role)
        => TenantRolePermissions.IsGranted(role, Contracts.TenantPermission.ManageTeam);

    public static bool CanAssignRole(TenantRole actorRole, TenantRole targetRole)
        => actorRole == TenantRole.Owner
           || (actorRole == TenantRole.TenantAdmin && targetRole != TenantRole.Owner);

    public static bool TryParse(string? value, out TenantRole role)
    {
        role = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = string.Equals(value.Trim(), "Admin", StringComparison.OrdinalIgnoreCase)
            ? nameof(TenantRole.TenantAdmin)
            : value.Trim();

        return Enum.TryParse(normalized, ignoreCase: true, out role)
               && Enum.IsDefined(role);
    }
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

    public string? BusinessContactEmail { get; private set; }

    public string DefaultCurrency { get; private set; } = "USD";

    public string TimeZone { get; private set; } = "UTC";

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Tenant Create(string name, string slug, DateTimeOffset? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Tenant name is required.");
        }

        var normalizedSlug = NormalizeSlug(slug);
        if (!IsValidSlug(normalizedSlug))
        {
            throw new DomainException("Tenant slug is invalid or reserved.");
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

    public void UpdateSettings(
        string? name,
        string? businessContactEmail,
        string? defaultCurrency,
        string? timeZone,
        AuditActor actor)
    {
        if (name is not null)
        {
            Rename(name, actor);
        }

        var changed = false;

        if (businessContactEmail is not null)
        {
            var trimmed = businessContactEmail.Trim();
            string? email = trimmed.Length == 0 ? null : trimmed;

            if (email is not null
                && (email.Length > 320
                    || !MailAddress.TryCreate(email, out var address)
                    || !string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase)))
            {
                throw new DomainException("Business contact email is invalid.");
            }

            if (!string.Equals(BusinessContactEmail, email, StringComparison.Ordinal))
            {
                BusinessContactEmail = email;
                changed = true;
            }
        }

        if (defaultCurrency is not null)
        {
            var currency = defaultCurrency.Trim().ToUpperInvariant();

            if (currency.Length != 3 || !currency.All(char.IsLetter))
            {
                throw new DomainException("Default currency must be a three-letter ISO code.");
            }

            if (!string.Equals(DefaultCurrency, currency, StringComparison.Ordinal))
            {
                DefaultCurrency = currency;
                changed = true;
            }
        }

        if (timeZone is not null)
        {
            var zone = timeZone.Trim();

            if (!TimeZoneInfo.TryFindSystemTimeZoneById(zone, out _))
            {
                throw new DomainException("Time zone is invalid.");
            }

            if (!string.Equals(TimeZone, zone, StringComparison.Ordinal))
            {
                TimeZone = zone;
                changed = true;
            }
        }

        if (changed)
        {
            UpdatedAt = DateTimeOffset.UtcNow;
            Raise(new TenantSettingsUpdated(
                Id,
                BusinessContactEmail,
                DefaultCurrency,
                TimeZone,
                actor));
        }
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

    private static readonly HashSet<string> ReservedSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin",
        "api",
        "app",
        "mail",
        "platform",
        "support",
        "www"
    };

    public static string NormalizeSlug(string slug)
        => slug.Trim().ToLowerInvariant();

    public static bool IsValidSlug(string slug)
    {
        // Limit tenant slugs
        var normalized = NormalizeSlug(slug);
        return normalized.Length is >= 2 and <= 40
               && !ReservedSlugs.Contains(normalized)
               && normalized.All(character =>
                   (character >= 'a' && character <= 'z')
                   || (character >= '0' && character <= '9')
                   || character == '-')
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
