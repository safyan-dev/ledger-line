namespace PaymentReconciliation.Modules.Webhooks.Application;

public sealed class WebhookIngestionOptions
{
    public double AckBudgetMs { get; set; } = 5;

    public long MaxPayloadBytes { get; set; } = 1_048_576;

    public int StuckMinutes { get; set; } = 5;

    public int OutboxBatchSize { get; set; } = 20;

    public int OutboxPollMilliseconds { get; set; } = 500;

    public int MaxAttempts { get; set; } = 5;

    public int RetryBaseSeconds { get; set; } = 5;

    public bool AllowSimulationHeaders { get; set; }
}

public sealed record WebhookEnvelope(
    string Provider,
    string RawBody,
    string? Signature,
    DateTimeOffset ReceivedAt,
    Guid TenantId,
    bool SimulateFailure = false,
    string? CorrelationId = null);

public enum WebhookClaimKind
{
    Claimed,
    Duplicate,
    Conflict
}

public sealed record WebhookClaim(
    WebhookClaimKind Kind,
    Guid? WebhookEventId,
    string? Reason);

public interface IWebhookEventClaimStore
{
    Task<WebhookClaim> ClaimAsync(WebhookEnvelope envelope, CancellationToken cancellationToken = default);
}
