namespace PaymentReconciliation.Modules.Reconciliation.Contracts;

public sealed record ReconciliationRecordDto(
    Guid Id,
    Guid PaymentId,
    string? ProviderPaymentId,
    Guid? OrderId,
    string? OrderNumber,
    string Outcome,
    string Strategy,
    decimal? ExpectedAmount,
    decimal ActualAmount,
    decimal Difference,
    string Currency,
    string? Reason,
    string? Notes,
    string MatchedBy,
    DateTimeOffset CreatedAt);
