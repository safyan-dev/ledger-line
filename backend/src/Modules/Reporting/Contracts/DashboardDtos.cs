namespace PaymentReconciliation.Modules.Reporting.Contracts;

public sealed record DashboardKpisDto(
    decimal MatchedRate7d,
    decimal UnmatchedAmount,
    int PaymentsToday,
    int PendingReview);

public sealed record DashboardFlowDto(
    int OrdersCreated,
    int WebhooksReceived,
    int PaymentsRecorded,
    int SuccessfulPayments,
    int Matched,
    int Unmatched,
    int ManualMatches);

public sealed record VolumePointDto(
    DateOnly Date,
    decimal MatchedAmount,
    decimal UnmatchedAmount,
    decimal ManualAmount);

public sealed record ReviewQueueItemDto(
    Guid PaymentId,
    string ProviderPaymentId,
    decimal Amount,
    string Currency,
    string? CustomerName,
    string? Reference,
    DateTimeOffset ReceivedAt,
    int AgeHours);

public sealed record ActivityItemDto(
    Guid Id,
    string ActorName,
    string Action,
    string Module,
    string EntityType,
    Guid? EntityId,
    string Summary,
    DateTimeOffset OccurredAt);

public sealed record DashboardSummaryDto(
    DashboardKpisDto Kpis,
    DashboardFlowDto Flow,
    IReadOnlyList<VolumePointDto> Volume,
    IReadOnlyList<ReviewQueueItemDto> ReviewQueue,
    IReadOnlyList<ActivityItemDto> RecentActivity);
