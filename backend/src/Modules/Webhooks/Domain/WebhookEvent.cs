using PaymentReconciliation.Modules.Webhooks.Contracts.Events;
using PaymentReconciliation.SharedKernel.Domain;

namespace PaymentReconciliation.Modules.Webhooks.Domain;

public enum WebhookStatus
{
    Received,
    Processing,
    Processed,
    Duplicate,
    Failed,
    Ignored,
    Conflict
}

public sealed class WebhookEvent : AggregateRoot
{
    private WebhookEvent()
    {
    }

    private WebhookEvent(
        Guid id,
        string provider,
        string providerEventId,
        string eventType,
        string payload,
        string? signature,
        string payloadHash,
        DateTimeOffset receivedAt)
        : base(id)
    {
        Provider = provider;
        ProviderEventId = providerEventId;
        EventType = eventType;
        Payload = payload;
        Signature = signature;
        PayloadHash = payloadHash;
        ReceivedAt = receivedAt;
        Status = WebhookStatus.Received;
        AttemptCount = 1;
    }

    public string Provider { get; private set; } = string.Empty;

    public string ProviderEventId { get; private set; } = string.Empty;

    public string EventType { get; private set; } = string.Empty;

    public string Payload { get; private set; } = "{}";

    public string? Signature { get; private set; }

    public string PayloadHash { get; private set; } = string.Empty;

    public WebhookStatus Status { get; private set; }

    public Guid? PaymentId { get; private set; }

    public string? FailureReason { get; private set; }

    public int AttemptCount { get; private set; }

    public int DuplicateCount { get; private set; }

    public int ConflictCount { get; private set; }

    public string? LastConflictReason { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public static WebhookEvent Create(
        string provider,
        string providerEventId,
        string eventType,
        string payload,
        string? signature,
        DateTimeOffset receivedAt,
        string? payloadHash = null)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new DomainException("Webhook provider is required.");
        }

        if (string.IsNullOrWhiteSpace(providerEventId))
        {
            throw new DomainException("Provider event id is required.");
        }

        var webhookEvent = new WebhookEvent(
            Guid.NewGuid(),
            provider.Trim().ToLowerInvariant(),
            providerEventId.Trim(),
            eventType.Trim(),
            payload,
            signature,
            payloadHash ?? string.Empty,
            receivedAt);

        webhookEvent.Raise(new WebhookEventReceived(
            webhookEvent.Id,
            webhookEvent.Provider,
            webhookEvent.ProviderEventId,
            webhookEvent.EventType));

        return webhookEvent;
    }

    public void MarkProcessing()
        => Status = WebhookStatus.Processing;

    public void MarkProcessed(Guid paymentId, DateTimeOffset? processedAt = null)
    {
        Status = WebhookStatus.Processed;
        PaymentId = paymentId;
        ProcessedAt = processedAt ?? DateTimeOffset.UtcNow;
        FailureReason = null;
        Raise(new WebhookEventProcessed(Id, Provider, ProviderEventId, paymentId));
    }

    public void MarkFailed(string reason, DateTimeOffset? processedAt = null)
    {
        Status = WebhookStatus.Failed;
        FailureReason = reason;
        ProcessedAt = processedAt ?? DateTimeOffset.UtcNow;
        AttemptCount++;
        Raise(new WebhookEventFailed(Id, Provider, ProviderEventId, reason));
    }

    public void MarkIgnored(string reason, DateTimeOffset? processedAt = null)
    {
        Status = WebhookStatus.Ignored;
        FailureReason = reason;
        ProcessedAt = processedAt ?? DateTimeOffset.UtcNow;
        Raise(new WebhookEventIgnored(Id, Provider, ProviderEventId, reason));
    }

    public void MarkConflict(string reason, DateTimeOffset? processedAt = null)
    {
        Status = WebhookStatus.Conflict;
        FailureReason = reason;
        ConflictCount++;
        LastConflictReason = reason;
        ProcessedAt = processedAt ?? DateTimeOffset.UtcNow;
        Raise(new WebhookEventConflict(Id, Provider, ProviderEventId, reason));
    }

    public void MarkRetry()
    {
        Status = WebhookStatus.Received;
        FailureReason = null;
        ProcessedAt = null;
    }

    public void RegisterDuplicate()
        => DuplicateCount++;

    public void RegisterConflict(string reason)
    {
        ConflictCount++;
        LastConflictReason = reason;
        Raise(new WebhookEventConflict(Id, Provider, ProviderEventId, reason));
    }
}
