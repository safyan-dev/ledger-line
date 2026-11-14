using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Domain;

namespace PaymentReconciliation.Modules.Support.Contracts.Events;

public sealed record SupportTicketCreated(
    Guid TenantId,
    Guid TicketId,
    string Subject,
    string Category,
    string Priority,
    AuditActor Actor) : DomainEvent;

public sealed record SupportTicketReplied(
    Guid TenantId,
    Guid TicketId,
    Guid MessageId,
    AuditActor Actor) : DomainEvent;

public sealed record SupportTicketStatusChanged(
    Guid TenantId,
    Guid TicketId,
    string PreviousStatus,
    string NewStatus,
    AuditActor Actor) : DomainEvent;
