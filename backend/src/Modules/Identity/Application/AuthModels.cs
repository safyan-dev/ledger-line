namespace PaymentReconciliation.Modules.Identity.Application;

public sealed record RegisterRequest(string Email, string Password, string DisplayName);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);

public sealed record AuthUserDto(Guid Id, string Email, string DisplayName, bool IsPlatformAdmin);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    AuthUserDto User);

public sealed record ForgotPasswordResponse(string? ResetToken);
