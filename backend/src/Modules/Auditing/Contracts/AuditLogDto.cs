namespace PaymentReconciliation.Modules.Auditing.Contracts;

public sealed record AuditLogDto(
    Guid Id,
    Guid? ActorId,
    string ActorName,
    string ActorRole,
    string Action,
    string Module,
    string EntityType,
    Guid? EntityId,
    string Summary,
    string? Metadata,
    string? CorrelationId,
    DateTimeOffset OccurredAt);
