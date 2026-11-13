namespace PaymentReconciliation.Modules.Reconciliation.Infrastructure;

using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Reconciliation.Contracts;
using PaymentReconciliation.Modules.Reconciliation.Domain;
using PaymentReconciliation.Persistence.Abstractions;

public sealed class ReconciliationReporting(IAppDbContext dbContext) : IReconciliationReporting
{
    public async Task<ReconciliationReportingSnapshot> GetSnapshotAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        var recent = await dbContext.Set<ReconciliationRecord>()
            .AsNoTracking()
            .Where(record => record.CreatedAt >= since)
            .Select(record => new ReconciliationReportingRow(
                record.Outcome.ToString(),
                record.Strategy.ToString(),
                record.ActualAmount,
                record.CreatedAt))
            .ToListAsync(cancellationToken);

        var manualMatches = await dbContext.Set<ReconciliationRecord>()
            .AsNoTracking()
            .CountAsync(record => record.Strategy == MatchStrategy.Manual, cancellationToken);

        return new ReconciliationReportingSnapshot(recent, manualMatches);
    }
}
