using PaymentReconciliation.Modules.Auditing.Contracts;
using PaymentReconciliation.Modules.Orders.Contracts;
using PaymentReconciliation.Modules.Payments.Contracts;
using PaymentReconciliation.Modules.Reconciliation.Contracts;
using PaymentReconciliation.Modules.Reporting.Contracts;
using PaymentReconciliation.Modules.Webhooks.Contracts;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Reporting.Application;

public sealed record GetDashboardSummaryQuery : IQuery<DashboardSummaryDto>;

public sealed class GetDashboardSummaryHandler(
    IOrderReporting orders,
    IPaymentReporting payments,
    IReconciliationReporting reconciliation,
    IWebhookReporting webhooks,
    IAuditReporting audit)
    : IQueryHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private static readonly HashSet<string> MatchedOutcomes =
    [
        "Matched",
        "PartiallyMatched",
        "ManuallyMatched"
    ];

    public async Task<Result<DashboardSummaryDto>> Handle(
        GetDashboardSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var since = now.AddDays(-14);
        var sevenDaysAgo = now.AddDays(-7);
        var todayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);

        var ordersCreated = await orders.CountAsync(cancellationToken);
        var webhooksReceived = await webhooks.CountAsync(cancellationToken);
        var paymentSnapshot = await payments.GetSnapshotAsync(since, todayStart, cancellationToken);
        var reconciliationSnapshot = await reconciliation.GetSnapshotAsync(since, cancellationToken);
        var recentAudit = await audit.GetRecentAsync(8, cancellationToken);

        var records = reconciliationSnapshot.Recent;

        var reviewQueue = paymentSnapshot.ReviewQueue
            .Select(item => new ReviewQueueItemDto(
                item.Id,
                item.ProviderPaymentId,
                item.Amount,
                item.Currency,
                item.CustomerName,
                item.Reference,
                item.ReceivedAt,
                Math.Max(0, (int)(now - item.ReceivedAt).TotalHours)))
            .ToList();

        var records7d = records.Where(record => record.CreatedAt >= sevenDaysAgo).ToList();
        var matched7d = records7d.Count(record => MatchedOutcomes.Contains(record.Outcome));
        var matchedRate = records7d.Count == 0
            ? 0m
            : decimal.Round(matched7d * 100m / records7d.Count, 1, MidpointRounding.AwayFromZero);

        var volume = Enumerable.Range(0, 14)
            .Select(offset => DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(-13 + offset)))
            .Select(date => new VolumePointDto(
                date,
                paymentSnapshot.Recent
                    .Where(payment => payment.Status == "Matched"
                                      && DateOnly.FromDateTime(payment.ReceivedAt.UtcDateTime.Date) == date)
                    .Sum(payment => payment.Amount),
                paymentSnapshot.Recent
                    .Where(payment => payment.Status == "Unmatched"
                                      && DateOnly.FromDateTime(payment.ReceivedAt.UtcDateTime.Date) == date)
                    .Sum(payment => payment.Amount),
                records
                    .Where(record => record.Strategy == "Manual"
                                     && DateOnly.FromDateTime(record.CreatedAt.UtcDateTime.Date) == date)
                    .Sum(record => record.ActualAmount)))
            .ToList();

        var activity = recentAudit
            .Select(item => new ActivityItemDto(
                item.Id,
                item.ActorName,
                item.Action,
                item.Module,
                item.EntityType,
                item.EntityId,
                item.Summary,
                item.OccurredAt))
            .ToList();

        var summary = new DashboardSummaryDto(
            new DashboardKpisDto(
                matchedRate,
                paymentSnapshot.UnmatchedAmount,
                paymentSnapshot.PaymentsToday,
                paymentSnapshot.PendingReview),
            new DashboardFlowDto(
                ordersCreated,
                webhooksReceived,
                paymentSnapshot.TotalRecorded,
                paymentSnapshot.Successful,
                paymentSnapshot.Matched,
                paymentSnapshot.PendingReview,
                reconciliationSnapshot.ManualMatches),
            volume,
            reviewQueue,
            activity);

        return Result.Success(summary);
    }
}
