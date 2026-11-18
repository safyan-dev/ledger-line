using PaymentReconciliation.Modules.Identity.Contracts;

namespace PaymentReconciliation.Modules.Tenancy.Application;

public sealed record TenantInvitationDto(
    Guid Id,
    string Email,
    string Role,
    string Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSentAt);

public sealed record IssuedTenantInvitationDto(
    TenantInvitationDto Invitation);

public sealed record CreateTenantInvitationRequest(string Email, string Role);

public sealed record AcceptTenantInvitationRequest(string Token, string? DisplayName, string? Password);

public sealed record AcceptTenantInvitationResponse(
    Guid TenantId,
    string TenantSlug,
    string TenantName,
    string Role,
    AuthSessionDto? Session);

public sealed record AuthSessionDto(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    IdentityUser User);
