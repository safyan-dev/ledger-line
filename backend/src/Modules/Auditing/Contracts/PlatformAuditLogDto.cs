namespace PaymentReconciliation.Modules.Auditing.Contracts;

public sealed record PlatformAuditLogDto(
    Guid Id,
    Guid? ActorId,
    string ActorName,
    string? ActorEmail,
    string ActorRole,
    string Action,
    string TargetType,
    Guid? TargetId,
    Guid? TenantId,
    string Summary,
    string? Metadata,
    string? CorrelationId,
    DateTimeOffset OccurredAt);
