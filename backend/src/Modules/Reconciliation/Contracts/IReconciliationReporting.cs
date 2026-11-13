namespace PaymentReconciliation.Modules.Reconciliation.Contracts;

public sealed record ReconciliationReportingRow(
    string Outcome,
    string Strategy,
    decimal ActualAmount,
    DateTimeOffset CreatedAt);

public sealed record ReconciliationReportingSnapshot(
    IReadOnlyList<ReconciliationReportingRow> Recent,
    int ManualMatches);

public interface IReconciliationReporting
{
    Task<ReconciliationReportingSnapshot> GetSnapshotAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken = default);
}
