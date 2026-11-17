using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PaymentReconciliation.Modules.Identity.Contracts;
using PaymentReconciliation.Modules.Identity.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Identity.Application;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    IAppDbContext dbContext,
    IUnitOfWork unitOfWork,
    IJwtTokenService jwtTokenService,
    IOptions<JwtOptions> jwtOptions,
    ICurrentUser currentUser,
    IEmailSender emailSender,
    IConfiguration configuration,
    IHostEnvironment environment,
    ITenantMembershipVerifier tenantMembershipVerifier,
    ILoginTenantResolver loginTenantResolver)
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<Result<RegisterResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await RegisterUserAsync(request.Email, request.Password, request.DisplayName, cancellationToken);
        if (user.IsFailure)
        {
            return Result.Failure<RegisterResponse>(user.Error!);
        }

        return Result.Success(new RegisterResponse(
            user.Value.User,
            user.Value.EmailVerificationToken));
    }

    public async Task<Result<RegisteredUser>> RegisterUserAsync(
        string email,
        string password,
        string displayName,
        CancellationToken cancellationToken = default,
        bool emailConfirmed = false)
    {
        if (!TryNormalizeEmail(email, out var normalizedEmail))
        {
            return Result.Failure<RegisteredUser>(Error.Validation(
                "auth.invalid_email",
                "A valid email address is required."));
        }

        var normalizedDisplayName = displayName?.Trim() ?? string.Empty;
        if (normalizedDisplayName.Length == 0 || normalizedDisplayName.Length > 160)
        {
            return Result.Failure<RegisteredUser>(Error.Validation(
                "auth.invalid_display_name",
                "Display name must be between 1 and 160 characters."));
        }

        if (await userManager.FindByEmailAsync(normalizedEmail) is not null)
        {
            return Result.Failure<RegisteredUser>(Error.Conflict(
                "auth.email_taken",
                "An account with this email already exists."));
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = normalizedEmail,
            Email = normalizedEmail,
            EmailConfirmed = emailConfirmed,
            DisplayName = normalizedDisplayName,
            Status = UserStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            return Result.Failure<RegisteredUser>(IdentityValidationError(
                createResult,
                "The account could not be created."));
        }

        var verificationToken = emailConfirmed
            ? null
            : await SendEmailConfirmationAsync(user, cancellationToken);

        return Result.Success(new RegisteredUser(ToDto(user), verificationToken));
    }

    public async Task<Result<AuthUserDto>> VerifyCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure<AuthUserDto>(Error.Unauthorized(
                "auth.invalid_credentials",
                "Email or password is invalid."));
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return LockedOut();
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            if (await userManager.IsLockedOutAsync(user))
            {
                return LockedOut();
            }

            return Result.Failure<AuthUserDto>(Error.Unauthorized(
                "auth.invalid_credentials",
                "Email or password is invalid."));
        }

        await userManager.ResetAccessFailedCountAsync(user);
        if (!user.EmailConfirmed)
        {
            return Result.Failure<AuthUserDto>(Error.Forbidden(
                "auth.email_not_confirmed",
                "Confirm your email address before signing in."));
        }

        return Result.Success(ToDto(user));
    }

    public async Task<Result<AuthUserDto>> ConfirmEmailAsync(
        ConfirmEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeEmail(request.Email, out var email))
        {
            return Result.Failure<AuthUserDto>(Error.Validation(
                "auth.invalid_email",
                "A valid email address is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Result.Failure<AuthUserDto>(Error.Validation(
                "auth.invalid_email_token",
                "The email confirmation token is invalid or has expired."));
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure<AuthUserDto>(Error.Validation(
                "auth.invalid_email_token",
                "The email confirmation token is invalid or has expired."));
        }

        if (user.EmailConfirmed)
        {
            return Result.Success(ToDto(user));
        }

        var result = await userManager.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded)
        {
            return Result.Failure<AuthUserDto>(Error.Validation(
                "auth.invalid_email_token",
                "The email confirmation token is invalid or has expired."));
        }

        return Result.Success(ToDto(user));
    }

    public async Task<Result<ResendConfirmationResponse>> ResendConfirmationAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeEmail(email, out var normalizedEmail))
        {
            return Result.Success(new ResendConfirmationResponse(null));
        }

        var user = await userManager.FindByEmailAsync(normalizedEmail);
        if (user is null || user.Status != UserStatus.Active || user.EmailConfirmed)
        {
            return Result.Success(new ResendConfirmationResponse(null));
        }

        var token = await SendEmailConfirmationAsync(user, cancellationToken);
        return Result.Success(new ResendConfirmationResponse(token));
    }

    public async Task<Result<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await VerifyCredentialsAsync(request.Email, request.Password, cancellationToken);
        if (user.IsFailure)
        {
            return Result.Failure<AuthResponse>(user.Error!);
        }

        var tenantSlug = NormalizeTenantSlug(request.TenantSlug)
                         ?? loginTenantResolver.ResolveTenantSlug();
        TenantLoginDto? tenant = null;

        if (tenantSlug is not null)
        {
            var membership = await tenantMembershipVerifier.FindForLoginAsync(
                user.Value.Id,
                tenantSlug,
                cancellationToken);

            if (membership is null)
            {
                return Result.Failure<AuthResponse>(Error.Forbidden(
                    "auth.tenant_membership_required",
                    "You are not a member of this workspace."));
            }

            tenant = new TenantLoginDto(
                membership.TenantId,
                membership.TenantName,
                membership.TenantSlug,
                membership.Role);
        }
        else if (!user.Value.IsPlatformAdmin)
        {
            return Result.Failure<AuthResponse>(Error.Forbidden(
                "auth.tenant_required",
                "Sign in at your workspace subdomain."));
        }

        var session = await IssueTokensAsync(user.Value.Id, cancellationToken);
        if (session.IsFailure)
        {
            return session;
        }

        return Result.Success(session.Value with { Tenant = tenant });
    }

    public async Task<Result<AuthResponse>> IssueTokensAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure<AuthResponse>(Error.Unauthorized(
                "auth.invalid_user",
                "User is not available."));
        }

        if (!user.EmailConfirmed)
        {
            return Result.Failure<AuthResponse>(Error.Forbidden(
                "auth.email_not_confirmed",
                "Confirm your email address before signing in."));
        }

        var accessToken = jwtTokenService.CreateAccessToken(user);
        var refreshToken = GenerateRefreshToken();
        var refreshExpiresAt = DateTimeOffset.UtcNow.AddDays(Math.Max(1, _jwtOptions.RefreshTokenDays));

        dbContext.Set<RefreshToken>().Add(RefreshToken.Create(
            user.Id,
            HashToken(refreshToken),
            refreshExpiresAt,
            Guid.NewGuid()));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new AuthResponse(
            accessToken.Token,
            refreshToken,
            accessToken.ExpiresAt,
            refreshExpiresAt,
            ToDto(user)));
    }

    public async Task<Result<AuthResponse>> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var tokenHash = HashToken(refreshToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var stored = await dbContext.Set<RefreshToken>()
            .FromSqlInterpolated($"SELECT * FROM identity.refresh_tokens WHERE token_hash = {tokenHash} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

        if (stored is null)
        {
            return InvalidRefreshToken();
        }

        if (!stored.IsActive(now))
        {
            if (stored.RevokedAt is not null && stored.ReplacedByTokenHash is not null)
            {
                await RevokeFamilyAsync(stored.UserId, stored.FamilyId, now, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            return InvalidRefreshToken();
        }

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null || user.Status != UserStatus.Active || !user.EmailConfirmed)
        {
            return InvalidRefreshToken();
        }

        var accessToken = jwtTokenService.CreateAccessToken(user);
        var replacement = GenerateRefreshToken();
        var refreshExpiresAt = now.AddDays(Math.Max(1, _jwtOptions.RefreshTokenDays));

        stored.Revoke(now);
        stored.MarkReplaced(HashToken(replacement));
        dbContext.Set<RefreshToken>().Add(RefreshToken.Create(
            user.Id,
            HashToken(replacement),
            refreshExpiresAt,
            stored.FamilyId));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success(new AuthResponse(
            accessToken.Token,
            replacement,
            accessToken.ExpiresAt,
            refreshExpiresAt,
            ToDto(user)));
    }

    public async Task<Result> LogoutAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var stored = await dbContext.Set<RefreshToken>()
            .FirstOrDefaultAsync(token => token.TokenHash == HashToken(refreshToken), cancellationToken);

        if (stored is null)
        {
            return Result.Success();
        }

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is not null)
        {
            user.SecurityStamp = Guid.NewGuid().ToString("N");
        }

        stored.Revoke();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<AuthUserDto>> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<AuthUserDto>(Error.Unauthorized(
                "auth.required",
                "Authentication is required."));
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure<AuthUserDto>(Error.Unauthorized(
                "auth.invalid_user",
                "User was not found."));
        }

        return Result.Success(ToDto(user));
    }

    public async Task<Result<AuthUserDto>> UpdateProfileAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<AuthUserDto>(Error.Unauthorized("auth.required", "Authentication is required."));
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure<AuthUserDto>(Error.Unauthorized("auth.invalid_user", "User was not found."));
        }

        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        if (displayName.Length == 0 || displayName.Length > 160)
        {
            return Result.Failure<AuthUserDto>(Error.Validation(
                "auth.invalid_display_name",
                "Display name must be between 1 and 160 characters."));
        }

        user.DisplayName = displayName;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return Result.Failure<AuthUserDto>(IdentityValidationError(
                updateResult,
                "The profile could not be updated."));
        }

        return Result.Success(ToDto(user));
    }

    public async Task<Result<ChangeEmailResponse>> RequestEmailChangeAsync(
        ChangeEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<ChangeEmailResponse>(Error.Unauthorized(
                "auth.required",
                "Authentication is required."));
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure<ChangeEmailResponse>(Error.Unauthorized(
                "auth.invalid_user",
                "User was not found."));
        }

        if (!TryNormalizeEmail(request.NewEmail, out var newEmail))
        {
            return Result.Failure<ChangeEmailResponse>(Error.Validation(
                "auth.invalid_email",
                "A valid email address is required."));
        }

        if (string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<ChangeEmailResponse>(Error.Validation(
                "auth.email_unchanged",
                "The new email address must be different from the current one."));
        }

        var existing = await userManager.FindByEmailAsync(newEmail);
        if (existing is not null && existing.Id != user.Id)
        {
            return Result.Failure<ChangeEmailResponse>(Error.Conflict(
                "auth.email_taken",
                "An account with this email already exists."));
        }

        var token = await userManager.GenerateChangeEmailTokenAsync(user, newEmail);

        await emailSender.SendAsync(
            new EmailMessage(
                newEmail,
                "Confirm your email change",
                $"Use this token to confirm your new email address: {token}"),
            cancellationToken);

        return Result.Success(new ChangeEmailResponse(
            environment.IsDevelopment() ? token : null));
    }

    public async Task<Result<AuthUserDto>> ConfirmEmailChangeAsync(
        ConfirmEmailChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<AuthUserDto>(Error.Unauthorized("auth.required", "Authentication is required."));
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure<AuthUserDto>(Error.Unauthorized("auth.invalid_user", "User was not found."));
        }

        if (!TryNormalizeEmail(request.NewEmail, out var newEmail))
        {
            return Result.Failure<AuthUserDto>(Error.Validation(
                "auth.invalid_email",
                "A valid email address is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Result.Failure<AuthUserDto>(Error.Validation(
                "auth.invalid_email_token",
                "The email change token is invalid or has expired."));
        }

        var changeResult = await userManager.ChangeEmailAsync(user, newEmail, request.Token);
        if (!changeResult.Succeeded)
        {
            if (changeResult.Errors.Any(error =>
                    error.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                return Result.Failure<AuthUserDto>(Error.Conflict(
                    "auth.email_taken",
                    "An account with this email already exists."));
            }

            return Result.Failure<AuthUserDto>(Error.Validation(
                "auth.invalid_email_token",
                "The email change token is invalid or has expired."));
        }

        user.UserName = newEmail;
        user.EmailConfirmed = true;

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return Result.Failure<AuthUserDto>(IdentityValidationError(
                updateResult,
                "The email could not be updated."));
        }

        await RevokeAllAsync(user.Id, cancellationToken);
        return Result.Success(ToDto(user));
    }

    public async Task<Result> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure(Error.Unauthorized("auth.required", "Authentication is required."));
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure(Error.Unauthorized("auth.invalid_user", "User was not found."));
        }

        var changeResult = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!changeResult.Succeeded)
        {
            return Result.Failure(IdentityValidationError(changeResult, "The password could not be changed."));
        }

        await RevokeAllAsync(user.Id, cancellationToken);
        return Result.Success();
    }

    public async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tokens = await dbContext.Set<RefreshToken>()
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.Revoke();
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is not null)
        {
            user.SecurityStamp = Guid.NewGuid().ToString("N");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task RevokeFamilyAsync(
        Guid userId,
        Guid familyId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var tokens = await dbContext.Set<RefreshToken>()
            .Where(token =>
                token.UserId == userId
                && token.FamilyId == familyId
                && token.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.Revoke(now);
        }
    }

    private async Task<string?> SendEmailConfirmationAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var baseUrl = configuration["Web:PublicBaseUrl"]?.TrimEnd('/');
        var email = user.Email ?? string.Empty;
        var body = baseUrl is null
            ? $"Confirm your email address with this token: {token}"
            : $"Confirm your email address: {baseUrl}/confirm-email?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";

        await emailSender.SendAsync(
            new EmailMessage(email, "Confirm your email address", body),
            cancellationToken);

        return environment.IsDevelopment() ? token : null;
    }

    private static AuthUserDto ToDto(ApplicationUser user)
        => new(user.Id, user.Email ?? string.Empty, user.DisplayName, user.IsPlatformAdmin, user.EmailConfirmed);

    private static string? NormalizeTenantSlug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var slug = value.Trim().ToLowerInvariant();
        if (slug.Length is < 2 or > 40)
        {
            return null;
        }

        if (!slug.All(character => char.IsLetterOrDigit(character) || character == '-'))
        {
            return null;
        }

        return char.IsLetterOrDigit(slug[0]) && char.IsLetterOrDigit(slug[^1]) ? slug : null;
    }

    private static bool TryNormalizeEmail(string? value, out string email)
    {
        email = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        if (candidate.Length > 320 || !MailAddress.TryCreate(candidate, out var address))
        {
            return false;
        }

        if (!string.Equals(address.Address, candidate, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        email = candidate;
        return true;
    }

    private static string GenerateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static Result<AuthUserDto> LockedOut()
        => Result.Failure<AuthUserDto>(Error.Unauthorized(
            "auth.account_locked",
            "Too many failed sign-in attempts. Try again later."));

    private static Result<AuthResponse> InvalidRefreshToken()
        => Result.Failure<AuthResponse>(Error.Unauthorized(
            "auth.invalid_refresh_token",
            "Refresh token is invalid or expired."));

    private static Error IdentityValidationError(IdentityResult result, string message)
    {
        var failures = result.Errors
            .GroupBy(error => error.Code)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());
        return Error.Validation("auth.identity_validation", message, failures);
    }
}
