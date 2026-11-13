using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Domain;

namespace PaymentReconciliation.Modules.Tenancy.Contracts.Events;

public sealed record TenantRenamed(Guid TenantId, string Name, AuditActor Actor) : DomainEvent;

public sealed record TenantMemberAdded(Guid TenantId, Guid UserId, string Role, AuditActor Actor) : DomainEvent;

public sealed record TenantMemberRoleChanged(
    Guid TenantId,
    Guid UserId,
    string PreviousRole,
    string NewRole,
    AuditActor Actor) : DomainEvent;

public sealed record TenantMemberRemoved(Guid TenantId, Guid UserId, string Role, AuditActor Actor) : DomainEvent;
