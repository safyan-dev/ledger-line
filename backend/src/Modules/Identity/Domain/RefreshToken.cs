namespace PaymentReconciliation.Modules.Identity.Domain;

public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    private RefreshToken(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt,
        Guid familyId)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
        FamilyId = familyId;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid FamilyId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? ReplacedByTokenHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public static RefreshToken Create(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        Guid familyId)
        => new(Guid.NewGuid(), userId, tokenHash, expiresAt, DateTimeOffset.UtcNow, familyId);

    public void Revoke(DateTimeOffset? revokedAt = null)
    {
        if (RevokedAt is null)
        {
            RevokedAt = revokedAt ?? DateTimeOffset.UtcNow;
        }
    }

    public void MarkReplaced(string replacementTokenHash) => ReplacedByTokenHash = replacementTokenHash;
}
