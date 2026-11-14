using PaymentReconciliation.Modules.Tenancy.Contracts.Events;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Domain;

namespace PaymentReconciliation.Modules.Tenancy.Domain;

public enum InvitationStatus
{
    Pending,
    Accepted,
    Expired,
    Revoked
}

public sealed class TenantInvitation : AggregateRoot, ITenantOwned
{
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(7);

    private TenantInvitation()
    {
    }

    public Guid TenantId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public TenantRole Role { get; private set; }

    public InvitationStatus Status { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public Guid InvitedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset LastSentAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public Guid? AcceptedByUserId { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsExpired(DateTimeOffset now)
        => Status == InvitationStatus.Pending && ExpiresAt <= now;

    public InvitationStatus EffectiveStatus(DateTimeOffset now)
        => IsExpired(now) ? InvitationStatus.Expired : Status;

    public static TenantInvitation Create(
        Guid tenantId,
        string email,
        TenantRole role,
        string tokenHash,
        DateTimeOffset expiresAt,
        Guid invitedByUserId,
        DateTimeOffset now,
        AuditActor actor)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainException("Invitations require a tenant.");
        }

        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail.Length == 0)
        {
            throw new DomainException("Invitation email is required.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("Invitation token is required.");
        }

        if (expiresAt <= now)
        {
            throw new DomainException("Invitation expiry must be in the future.");
        }

        var invitation = new TenantInvitation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = normalizedEmail,
            Role = role,
            Status = InvitationStatus.Pending,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            InvitedByUserId = invitedByUserId,
            CreatedAt = now,
            LastSentAt = now
        };

        invitation.Raise(new TenantInvitationCreated(
            tenantId,
            invitation.Id,
            invitation.Email,
            role.ToString(),
            actor));

        return invitation;
    }

    public void Resend(string tokenHash, DateTimeOffset expiresAt, DateTimeOffset now, AuditActor actor)
    {
        if (Status is InvitationStatus.Accepted or InvitationStatus.Revoked)
        {
            throw new ConflictException(
                "invitation.not_pending",
                "Only a pending or expired invitation can be resent.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("Invitation token is required.");
        }

        if (expiresAt <= now)
        {
            throw new DomainException("Invitation expiry must be in the future.");
        }

        Status = InvitationStatus.Pending;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        LastSentAt = now;
        RevokedAt = null;
        Raise(new TenantInvitationResent(TenantId, Id, Email, expiresAt, actor));
    }

    public void Revoke(DateTimeOffset now, AuditActor actor)
    {
        if (Status == InvitationStatus.Accepted)
        {
            throw new ConflictException(
                "invitation.already_accepted",
                "An accepted invitation cannot be revoked.");
        }

        if (Status == InvitationStatus.Revoked)
        {
            return;
        }

        Status = InvitationStatus.Revoked;
        RevokedAt = now;
        Raise(new TenantInvitationRevoked(TenantId, Id, Email, actor));
    }

    public void Accept(Guid userId, DateTimeOffset now, AuditActor actor)
    {
        if (Status == InvitationStatus.Accepted)
        {
            throw new ConflictException(
                "invitation.already_accepted",
                "This invitation has already been accepted.");
        }

        if (Status == InvitationStatus.Revoked)
        {
            throw new ConflictException(
                "invitation.revoked",
                "This invitation has been revoked.");
        }

        if (IsExpired(now))
        {
            throw new ConflictException(
                "invitation.expired",
                "This invitation has expired.");
        }

        Status = InvitationStatus.Accepted;
        AcceptedAt = now;
        AcceptedByUserId = userId;
        Raise(new TenantInvitationAccepted(TenantId, Id, userId, Role.ToString(), actor));
    }

    public static string NormalizeEmail(string email)
        => email.Trim().ToLowerInvariant();
}
