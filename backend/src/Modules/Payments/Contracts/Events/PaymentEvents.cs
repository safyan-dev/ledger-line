using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Domain;

namespace PaymentReconciliation.Modules.Payments.Contracts.Events;

public sealed record PaymentRecorded(
    Guid PaymentId,
    string Provider,
    string ProviderPaymentId,
    Guid? OrderId,
    decimal Amount,
    string Currency,
    string? Reference,
    string? CustomerReference,
    DateTimeOffset ReceivedAt,
    AuditActor Actor) : DomainEvent;

public sealed record PaymentLinkedToOrder(
    Guid PaymentId,
    Guid OrderId,
    AuditActor Actor) : DomainEvent;

public sealed record PaymentMatched(
    Guid PaymentId,
    Guid OrderId,
    AuditActor Actor) : DomainEvent;

public sealed record PaymentMarkedUnmatched(
    Guid PaymentId,
    string Reason,
    AuditActor Actor) : DomainEvent;

public sealed record PaymentFailed(
    Guid PaymentId,
    string Reason,
    AuditActor Actor) : DomainEvent;

public sealed record PaymentRefunded(
    Guid RefundId,
    Guid PaymentId,
    Guid? OrderId,
    decimal Amount,
    string Currency,
    string Reason,
    AuditActor Actor) : DomainEvent;
