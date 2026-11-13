namespace PaymentReconciliation.Modules.Auditing.Contracts;

public sealed record AuditActivityItem(
    Guid Id,
    string ActorName,
    string Action,
    string Module,
    string EntityType,
    Guid? EntityId,
    string Summary,
    DateTimeOffset OccurredAt);

public interface IAuditReporting
{
    Task<IReadOnlyList<AuditActivityItem>> GetRecentAsync(
        int take,
        CancellationToken cancellationToken = default);
}
