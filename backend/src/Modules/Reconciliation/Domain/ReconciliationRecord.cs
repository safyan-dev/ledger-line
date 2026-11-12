using PaymentReconciliation.Modules.Reconciliation.Contracts.Events;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Domain;

namespace PaymentReconciliation.Modules.Reconciliation.Domain;

public enum ReconciliationOutcome
{
    Matched,
    PartiallyMatched,
    AmountMismatch,
    Overpaid,
    Unmatched,
    Ambiguous,
    ManuallyMatched
}

public enum MatchStrategy
{
    ExactReference,
    CustomerAndAmount,
    AmountAndDate,
    OrderId,
    Manual,
    None
}

public sealed class ReconciliationRecord : AggregateRoot
{
    private ReconciliationRecord()
    {
    }

    private ReconciliationRecord(
        Guid id,
        Guid paymentId,
        Guid? orderId,
        ReconciliationOutcome outcome,
        MatchStrategy strategy,
        decimal? expectedAmount,
        decimal actualAmount,
        string currency,
        decimal difference,
        string? reason,
        string? notes,
        string matchedBy,
        DateTimeOffset createdAt)
        : base(id)
    {
        PaymentId = paymentId;
        OrderId = orderId;
        Outcome = outcome;
        Strategy = strategy;
        ExpectedAmount = expectedAmount;
        ActualAmount = actualAmount;
        Currency = currency;
        Difference = difference;
        Reason = reason;
        Notes = notes;
        MatchedBy = matchedBy;
        CreatedAt = createdAt;
    }

    public Guid PaymentId { get; private set; }

    public Guid? OrderId { get; private set; }

    public ReconciliationOutcome Outcome { get; private set; }

    public MatchStrategy Strategy { get; private set; }

    public decimal? ExpectedAmount { get; private set; }

    public decimal ActualAmount { get; private set; }

    public decimal Difference { get; private set; }

    public string Currency { get; private set; } = "USD";

    public string? Reason { get; private set; }

    public string? Notes { get; private set; }

    public string MatchedBy { get; private set; } = "system";

    public DateTimeOffset CreatedAt { get; private set; }

    public static ReconciliationRecord Create(
        Guid paymentId,
        Guid? orderId,
        ReconciliationOutcome outcome,
        MatchStrategy strategy,
        decimal? expectedAmount,
        decimal actualAmount,
        string currency,
        string? reason,
        string? notes,
        AuditActor actor,
        DateTimeOffset? createdAt = null)
    {
        if (paymentId == Guid.Empty)
        {
            throw new DomainException("A reconciliation record requires a payment.");
        }

        if (actualAmount <= 0)
        {
            throw new DomainException("Reconciled payment amount must be greater than zero.");
        }

        var difference = expectedAmount is null
            ? 0m
            : decimal.Round(actualAmount - expectedAmount.Value, 2, MidpointRounding.AwayFromZero);

        var record = new ReconciliationRecord(
            Guid.NewGuid(),
            paymentId,
            orderId,
            outcome,
            strategy,
            expectedAmount,
            actualAmount,
            currency,
            difference,
            reason,
            notes,
            actor.Name,
            createdAt ?? DateTimeOffset.UtcNow);

        record.Raise(new ReconciliationCompleted(
            record.Id,
            paymentId,
            orderId,
            outcome.ToString(),
            strategy.ToString(),
            actualAmount,
            expectedAmount,
            difference,
            currency,
            actor));

        return record;
    }
}
