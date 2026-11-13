namespace PaymentReconciliation.Modules.Webhooks.Application;

public sealed class WebhookIngestionOptions
{
    public int QueueCapacity { get; set; } = 10_000;

    public int WorkerCount { get; set; } = 4;

    public double AckBudgetMs { get; set; } = 5;

    public long MaxPayloadBytes { get; set; } = 1_048_576;

    public int StuckMinutes { get; set; } = 5;

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
