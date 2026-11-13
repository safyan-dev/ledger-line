namespace PaymentReconciliation.Modules.Payments.Contracts;

public sealed record PaymentReportingRow(
    Guid Id,
    string Status,
    decimal Amount,
    string Currency,
    DateTimeOffset ReceivedAt,
    string ProviderPaymentId,
    string? CustomerName,
    string? Reference);

public sealed record PaymentReportingSnapshot(
    IReadOnlyList<PaymentReportingRow> Recent,
    int TotalRecorded,
    int Successful,
    int Matched,
    int PendingReview,
    decimal UnmatchedAmount,
    int PaymentsToday,
    IReadOnlyList<PaymentReportingRow> ReviewQueue);

public interface IPaymentReporting
{
    Task<PaymentReportingSnapshot> GetSnapshotAsync(
        DateTimeOffset since,
        DateTimeOffset todayStart,
        CancellationToken cancellationToken = default);
}
