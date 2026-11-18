namespace PaymentReconciliation.Modules.Identity.Application;

public sealed record RegisterRequest(string Email, string Password, string DisplayName);

public sealed record ConfirmEmailRequest(string Email, string Token);

public sealed record ResendConfirmationRequest(string Email);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);

public sealed record AuthUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsPlatformAdmin,
    bool EmailConfirmed);

public sealed record TenantLoginDto(
    Guid TenantId,
    string TenantName,
    string TenantSlug,
    string Role);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    AuthUserDto User,
    TenantLoginDto? Tenant = null);

public sealed record ForgotPasswordResponse();

public sealed record UpdateProfileRequest(string DisplayName);

public sealed record ChangeEmailRequest(string NewEmail);

public sealed record ConfirmEmailChangeRequest(string NewEmail, string Token);

public sealed record ChangeEmailResponse();
