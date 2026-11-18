using Microsoft.AspNetCore.Identity;
using PaymentReconciliation.Modules.Identity.Application;
using PaymentReconciliation.Modules.Identity.Contracts;
using PaymentReconciliation.Modules.Identity.Domain;
using PaymentReconciliation.SharedKernel.Results;
using IdentityUser = PaymentReconciliation.Modules.Identity.Contracts.IdentityUser;

namespace PaymentReconciliation.Modules.Identity.Infrastructure;

public sealed class IdentityFacade(
    AuthService authService,
    UserManager<ApplicationUser> userManager) : IIdentityFacade
{
    public async Task<Result<IdentityUser>> CreateUserAsync(
        string email,
        string password,
        string displayName,
        CancellationToken cancellationToken = default,
        bool emailConfirmed = false)
    {
        var result = await authService.RegisterUserAsync(
            email,
            password,
            displayName,
            cancellationToken,
            emailConfirmed);
        return result.IsFailure
            ? Result.Failure<IdentityUser>(result.Error!)
            : Result.Success(ToContract(result.Value));
    }

    public async Task<Result<IdentityUser>> VerifyCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var result = await authService.VerifyCredentialsAsync(email, password, cancellationToken);
        return result.IsFailure
            ? Result.Failure<IdentityUser>(result.Error!)
            : Result.Success(ToContract(result.Value));
    }

    public async Task<Result<IdentityUser>> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        return user is null
            ? Result.Failure<IdentityUser>(Error.NotFound(
                "identity.user_not_found",
                "No account exists for this email address."))
            : Result.Success(ToContract(user));
    }

    public async Task<IReadOnlyList<IdentityUser>> GetUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var users = new List<IdentityUser>(userIds.Count);

        foreach (var userId in userIds)
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is not null)
            {
                users.Add(ToContract(user));
            }
        }

        return users;
    }

    public async Task<Result<IssuedSession>> IssueSessionAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var result = await authService.IssueTokensAsync(userId, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<IssuedSession>(result.Error!);
        }

        var session = result.Value;
        return Result.Success(new IssuedSession(
            session.AccessToken,
            session.RefreshToken,
            session.AccessTokenExpiresAt,
            session.RefreshTokenExpiresAt,
            ToContract(session.User)));
    }

    public async Task<Result> RevokeSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await authService.RevokeAllAsync(userId, cancellationToken);
        return Result.Success();
    }

    private static IdentityUser ToContract(AuthUserDto user)
        => new(user.Id, user.Email, user.DisplayName, user.IsPlatformAdmin, user.EmailConfirmed);

    private static IdentityUser ToContract(ApplicationUser user)
        => new(user.Id, user.Email ?? string.Empty, user.DisplayName, user.IsPlatformAdmin, user.EmailConfirmed);
}
