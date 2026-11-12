using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Auditing.Domain;
using PaymentReconciliation.Modules.Orders.Domain;
using PaymentReconciliation.Modules.Payments.Domain;
using PaymentReconciliation.Modules.Reconciliation.Domain;
using PaymentReconciliation.Modules.Reporting.Contracts;
using PaymentReconciliation.Modules.Webhooks.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Reporting.Application;

public sealed record GetDashboardSummaryQuery : IQuery<DashboardSummaryDto>;

public sealed class GetDashboardSummaryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private static readonly ReconciliationOutcome[] MatchedOutcomes =
    [
        ReconciliationOutcome.Matched,
        ReconciliationOutcome.PartiallyMatched,
        ReconciliationOutcome.ManuallyMatched
    ];

    public async Task<Result<DashboardSummaryDto>> Handle(
        GetDashboardSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var since = now.AddDays(-14);
        var sevenDaysAgo = now.AddDays(-7);
        var todayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);

        var payments = await dbContext.Set<Payment>()
            .AsNoTracking()
            .Where(payment => payment.ReceivedAt >= since)
            .Select(payment => new
            {
                payment.Id,
                payment.Status,
                payment.Amount,
                payment.Currency,
                payment.ReceivedAt,
                payment.ProviderPaymentId,
                payment.CustomerName,
                payment.Reference
            })
            .ToListAsync(cancellationToken);

        var records = await dbContext.Set<ReconciliationRecord>()
            .AsNoTracking()
            .Where(record => record.CreatedAt >= since)
            .Select(record => new
            {
                record.Outcome,
                record.Strategy,
                record.ActualAmount,
                record.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var webhooksReceived = await dbContext.Set<WebhookEvent>()
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var ordersCreated = await dbContext.Set<Order>()
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var paymentsRecorded = await dbContext.Set<Payment>()
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var successfulPayments = await dbContext.Set<Payment>()
            .AsNoTracking()
            .CountAsync(payment => payment.Status != PaymentStatus.Failed, cancellationToken);

        var matchedPayments = await dbContext.Set<Payment>()
            .AsNoTracking()
            .CountAsync(payment => payment.Status == PaymentStatus.Matched, cancellationToken);

        var manualMatches = await dbContext.Set<ReconciliationRecord>()
            .AsNoTracking()
            .CountAsync(record => record.Strategy == MatchStrategy.Manual, cancellationToken);

        var unmatchedQuery = dbContext.Set<Payment>()
            .AsNoTracking()
            .Where(payment => payment.Status == PaymentStatus.Unmatched);

        var pendingReview = await unmatchedQuery.CountAsync(cancellationToken);
        var unmatchedAmount = await unmatchedQuery.SumAsync(payment => payment.Amount, cancellationToken);

        var reviewQueue = await unmatchedQuery
            .OrderBy(payment => payment.ReceivedAt)
            .Take(5)
            .Select(payment => new ReviewQueueItemDto(
                payment.Id,
                payment.ProviderPaymentId,
                payment.Amount,
                payment.Currency,
                payment.CustomerName,
                payment.Reference,
                payment.ReceivedAt,
                0))
            .ToListAsync(cancellationToken);

        reviewQueue = reviewQueue
            .Select(item => item with { AgeHours = Math.Max(0, (int)(now - item.ReceivedAt).TotalHours) })
            .ToList();

        var paymentsToday = await dbContext.Set<Payment>()
            .AsNoTracking()
            .CountAsync(payment => payment.ReceivedAt >= todayStart, cancellationToken);

        var recentAudit = await dbContext.Set<AuditLog>()
            .AsNoTracking()
            .OrderByDescending(log => log.OccurredAt)
            .Take(8)
            .Select(log => new ActivityItemDto(
                log.Id,
                log.ActorName,
                log.Action,
                log.Module,
                log.EntityType,
                log.EntityId,
                log.Summary,
                log.OccurredAt))
            .ToListAsync(cancellationToken);

        var records7d = records.Where(record => record.CreatedAt >= sevenDaysAgo).ToList();
        var matched7d = records7d.Count(record => MatchedOutcomes.Contains(record.Outcome));
        var matchedRate = records7d.Count == 0
            ? 0m
            : decimal.Round(matched7d * 100m / records7d.Count, 1, MidpointRounding.AwayFromZero);

        var volume = Enumerable.Range(0, 14)
            .Select(offset => DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(-13 + offset)))
            .Select(date => new VolumePointDto(
                date,
                payments
                    .Where(payment => payment.Status == PaymentStatus.Matched
                                      && DateOnly.FromDateTime(payment.ReceivedAt.UtcDateTime.Date) == date)
                    .Sum(payment => payment.Amount),
                payments
                    .Where(payment => payment.Status == PaymentStatus.Unmatched
                                      && DateOnly.FromDateTime(payment.ReceivedAt.UtcDateTime.Date) == date)
                    .Sum(payment => payment.Amount),
                records
                    .Where(record => record.Strategy == MatchStrategy.Manual
                                     && DateOnly.FromDateTime(record.CreatedAt.UtcDateTime.Date) == date)
                    .Sum(record => record.ActualAmount)))
            .ToList();

        var summary = new DashboardSummaryDto(
            new DashboardKpisDto(
                matchedRate,
                unmatchedAmount,
                paymentsToday,
                pendingReview),
            new DashboardFlowDto(
                ordersCreated,
                webhooksReceived,
                paymentsRecorded,
                successfulPayments,
                matchedPayments,
                pendingReview,
                manualMatches),
            volume,
            reviewQueue,
            recentAudit);

        return Result.Success(summary);
    }
}
