namespace PaymentReconciliation.Modules.Webhooks.Contracts;

public sealed record WebhookEventListItemDto(
    Guid Id,
    string Provider,
    string ProviderEventId,
    string EventType,
    string Status,
    Guid? PaymentId,
    string? FailureReason,
    int AttemptCount,
    int DuplicateCount,
    int ConflictCount,
    string? LastConflictReason,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? ProcessedAt);

public sealed record WebhookEventDto(
    Guid Id,
    string Provider,
    string ProviderEventId,
    string EventType,
    string Status,
    string Payload,
    bool HasSignature,
    Guid? PaymentId,
    string? FailureReason,
    int AttemptCount,
    int DuplicateCount,
    int ConflictCount,
    string? LastConflictReason,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? ProcessedAt);

public sealed record WebhookAck(
    string Provider,
    DateTimeOffset ReceivedAt,
    bool Accepted);
