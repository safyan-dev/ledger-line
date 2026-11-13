namespace PaymentReconciliation.Modules.Payments.Infrastructure;

using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Payments.Contracts;
using PaymentReconciliation.Modules.Payments.Domain;
using PaymentReconciliation.Persistence.Abstractions;

public sealed class PaymentReporting(IAppDbContext dbContext) : IPaymentReporting
{
    public async Task<PaymentReportingSnapshot> GetSnapshotAsync(
        DateTimeOffset since,
        DateTimeOffset todayStart,
        CancellationToken cancellationToken = default)
    {
        var recent = await dbContext.Set<Payment>()
            .AsNoTracking()
            .Where(payment => payment.ReceivedAt >= since)
            .Select(payment => new PaymentReportingRow(
                payment.Id,
                payment.Status.ToString(),
                payment.Amount,
                payment.Currency,
                payment.ReceivedAt,
                payment.ProviderPaymentId,
                payment.CustomerName,
                payment.Reference))
            .ToListAsync(cancellationToken);

        var totalRecorded = await dbContext.Set<Payment>()
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var successful = await dbContext.Set<Payment>()
            .AsNoTracking()
            .CountAsync(payment => payment.Status != PaymentStatus.Failed, cancellationToken);

        var matched = await dbContext.Set<Payment>()
            .AsNoTracking()
            .CountAsync(payment => payment.Status == PaymentStatus.Matched, cancellationToken);

        var unmatchedQuery = dbContext.Set<Payment>()
            .AsNoTracking()
            .Where(payment => payment.Status == PaymentStatus.Unmatched);

        var pendingReview = await unmatchedQuery.CountAsync(cancellationToken);
        var unmatchedAmount = await unmatchedQuery.SumAsync(payment => payment.Amount, cancellationToken);

        var reviewQueue = await unmatchedQuery
            .OrderBy(payment => payment.ReceivedAt)
            .Take(5)
            .Select(payment => new PaymentReportingRow(
                payment.Id,
                payment.Status.ToString(),
                payment.Amount,
                payment.Currency,
                payment.ReceivedAt,
                payment.ProviderPaymentId,
                payment.CustomerName,
                payment.Reference))
            .ToListAsync(cancellationToken);

        var paymentsToday = await dbContext.Set<Payment>()
            .AsNoTracking()
            .CountAsync(payment => payment.ReceivedAt >= todayStart, cancellationToken);

        return new PaymentReportingSnapshot(
            recent,
            totalRecorded,
            successful,
            matched,
            pendingReview,
            unmatchedAmount,
            paymentsToday,
            reviewQueue);
    }
}
