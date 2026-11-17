using PaymentReconciliation.Modules.Identity.Contracts;

namespace PaymentReconciliation.Modules.Tenancy.Application;

public sealed record RegisterTenantRequest(
    string CompanyName,
    string TenantSlug,
    string OwnerName,
    string Email,
    string Password,
    string? PlanCode = null);

public sealed record TenantDto(Guid Id, string Name, string Slug, string Role);

public sealed record TenantSessionDto(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    IdentityUser User);

public sealed record RegisterTenantResponse(
    TenantSessionDto? Session,
    TenantDto Tenant,
    string PlanCode,
    string PlanName,
    bool EmailVerificationRequired,
    string? EmailVerificationToken);

public sealed record MembershipDto(Guid TenantId, string Name, string Slug, string Role);
