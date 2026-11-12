using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Domain;

namespace PaymentReconciliation.Modules.Orders.Contracts.Events;

public sealed record OrderCreated(
    Guid OrderId,
    string OrderNumber,
    string CustomerName,
    decimal ExpectedAmount,
    string Currency,
    AuditActor Actor) : DomainEvent;

public sealed record OrderCancelled(
    Guid OrderId,
    string OrderNumber,
    string Reason,
    AuditActor Actor) : DomainEvent;

public sealed record OrderPaymentAllocated(
    Guid OrderId,
    string OrderNumber,
    Guid PaymentId,
    decimal Amount,
    decimal PaidAmount,
    decimal ExpectedAmount,
    string Currency,
    string Status,
    AuditActor Actor) : DomainEvent;

public sealed record OrderFullyPaid(
    Guid OrderId,
    string OrderNumber,
    string Currency,
    decimal PaidAmount,
    AuditActor Actor) : DomainEvent;

public sealed record OrderOverpaid(
    Guid OrderId,
    string OrderNumber,
    string Currency,
    decimal PaidAmount,
    decimal ExpectedAmount,
    AuditActor Actor) : DomainEvent;

public sealed record OrderPaymentReversed(
    Guid OrderId,
    string OrderNumber,
    Guid PaymentId,
    decimal Amount,
    decimal PaidAmount,
    string Currency,
    string Status,
    AuditActor Actor) : DomainEvent;
