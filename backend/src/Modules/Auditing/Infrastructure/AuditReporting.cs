namespace PaymentReconciliation.Modules.Auditing.Infrastructure;

using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Auditing.Contracts;
using PaymentReconciliation.Modules.Auditing.Domain;
using PaymentReconciliation.Persistence.Abstractions;

public sealed class AuditReporting(IAppDbContext dbContext) : IAuditReporting
{
    public async Task<IReadOnlyList<AuditActivityItem>> GetRecentAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(take, 1, 50);

        return await dbContext.Set<AuditLog>()
            .AsNoTracking()
            .OrderByDescending(log => log.OccurredAt)
            .Take(limit)
            .Select(log => new AuditActivityItem(
                log.Id,
                log.ActorName,
                log.Action,
                log.Module,
                log.EntityType,
                log.EntityId,
                log.Summary,
                log.OccurredAt))
            .ToListAsync(cancellationToken);
    }
}
