using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Identity.Contracts;

public sealed record IdentityUser(Guid Id, string Email, string DisplayName, bool IsPlatformAdmin);

public sealed record IssuedSession(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    IdentityUser User);

public interface IIdentityFacade
{
    Task<Result<IdentityUser>> CreateUserAsync(
        string email,
        string password,
        string displayName,
        CancellationToken cancellationToken = default);

    Task<Result<IdentityUser>> VerifyCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<Result<IdentityUser>> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IdentityUser>> GetUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);

    Task<Result<IssuedSession>> IssueSessionAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result> RevokeSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
}
