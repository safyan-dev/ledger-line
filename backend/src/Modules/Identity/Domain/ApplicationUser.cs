using Microsoft.AspNetCore.Identity;

namespace PaymentReconciliation.Modules.Identity.Domain;

public enum UserStatus
{
    Active,
    Suspended
}

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public bool IsPlatformAdmin { get; set; }

    public UserStatus Status { get; set; } = UserStatus.Active;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
