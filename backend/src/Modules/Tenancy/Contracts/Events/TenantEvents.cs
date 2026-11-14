using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Domain;

namespace PaymentReconciliation.Modules.Tenancy.Contracts.Events;

public sealed record TenantRenamed(Guid TenantId, string Name, AuditActor Actor) : DomainEvent;

public sealed record TenantSettingsUpdated(
    Guid TenantId,
    string? BusinessContactEmail,
    string DefaultCurrency,
    string TimeZone,
    AuditActor Actor) : DomainEvent;

public sealed record TenantMemberAdded(Guid TenantId, Guid UserId, string Role, AuditActor Actor) : DomainEvent;

public sealed record TenantMemberRoleChanged(
    Guid TenantId,
    Guid UserId,
    string PreviousRole,
    string NewRole,
    AuditActor Actor) : DomainEvent;

public sealed record TenantMemberRemoved(Guid TenantId, Guid UserId, string Role, AuditActor Actor) : DomainEvent;

public sealed record TenantInvitationCreated(
    Guid TenantId,
    Guid InvitationId,
    string Email,
    string Role,
    AuditActor Actor) : DomainEvent;

public sealed record TenantInvitationResent(
    Guid TenantId,
    Guid InvitationId,
    string Email,
    DateTimeOffset ExpiresAt,
    AuditActor Actor) : DomainEvent;

public sealed record TenantInvitationAccepted(
    Guid TenantId,
    Guid InvitationId,
    Guid UserId,
    string Role,
    AuditActor Actor) : DomainEvent;

public sealed record TenantInvitationRevoked(
    Guid TenantId,
    Guid InvitationId,
    string Email,
    AuditActor Actor) : DomainEvent;
