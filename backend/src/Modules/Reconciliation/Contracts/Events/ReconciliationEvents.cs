using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Domain;

namespace PaymentReconciliation.Modules.Reconciliation.Contracts.Events;

public sealed record ReconciliationCompleted(
    Guid ReconciliationRecordId,
    Guid PaymentId,
    Guid? OrderId,
    string Outcome,
    string Strategy,
    decimal ActualAmount,
    decimal? ExpectedAmount,
    decimal Difference,
    string Currency,
    AuditActor Actor) : DomainEvent;
