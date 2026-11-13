using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
    ICurrentUser currentUser)
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<Result<AuthResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await RegisterUserAsync(request.Email, request.Password, request.DisplayName, cancellationToken);
        if (user.IsFailure)
        {
            return Result.Failure<AuthResponse>(user.Error!);
        }

        return await IssueTokensAsync(user.Value.Id, cancellationToken);
    }

    public async Task<Result<AuthUserDto>> RegisterUserAsync(
        string email,
        string password,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim();
        if (await userManager.FindByEmailAsync(normalizedEmail) is not null)
        {
            return Result.Failure<AuthUserDto>(Error.Conflict(
                "auth.email_taken",
                "An account with this email already exists."));
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = normalizedEmail,
            Email = normalizedEmail,
            EmailConfirmed = true,
            DisplayName = displayName.Trim(),
            Status = UserStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            return Result.Failure<AuthUserDto>(IdentityValidationError(
                createResult,
                "The account could not be created."));
        }

        return Result.Success(ToDto(user));
    }

    public async Task<Result<AuthUserDto>> VerifyCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null
            || user.Status != UserStatus.Active
            || !await userManager.CheckPasswordAsync(user, password))
        {
            return Result.Failure<AuthUserDto>(Error.Unauthorized(
                "auth.invalid_credentials",
                "Email or password is invalid."));
        }

        return Result.Success(ToDto(user));
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

        return await IssueTokensAsync(user.Value.Id, cancellationToken);
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

        var accessToken = jwtTokenService.CreateAccessToken(user);
        var refreshToken = GenerateRefreshToken();
        var refreshExpiresAt = DateTimeOffset.UtcNow.AddDays(Math.Max(1, _jwtOptions.RefreshTokenDays));

        dbContext.Set<RefreshToken>().Add(RefreshToken.Create(
            user.Id,
            HashToken(refreshToken),
            refreshExpiresAt));

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
        var stored = await dbContext.Set<RefreshToken>()
            .FirstOrDefaultAsync(token => token.TokenHash == HashToken(refreshToken), cancellationToken);

        if (stored is null || !stored.IsActive(DateTimeOffset.UtcNow))
        {
            return InvalidRefreshToken();
        }

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null || user.Status != UserStatus.Active)
        {
            return InvalidRefreshToken();
        }

        var accessToken = jwtTokenService.CreateAccessToken(user);
        var replacement = GenerateRefreshToken();
        var refreshExpiresAt = DateTimeOffset.UtcNow.AddDays(Math.Max(1, _jwtOptions.RefreshTokenDays));

        stored.Revoke();
        stored.MarkReplaced(HashToken(replacement));
        dbContext.Set<RefreshToken>().Add(RefreshToken.Create(
            user.Id,
            HashToken(replacement),
            refreshExpiresAt));

        await unitOfWork.SaveChangesAsync(cancellationToken);

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

    public async Task<Result> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure(Error.Unauthorized("auth.required", "Authentication is required."));
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
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

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static AuthUserDto ToDto(ApplicationUser user)
        => new(user.Id, user.Email ?? string.Empty, user.DisplayName, user.IsPlatformAdmin);

    private static string GenerateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

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
