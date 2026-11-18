namespace PaymentReconciliation.Modules.Webhooks.Application;

public sealed record WebhookOutboxItem(
    Guid WebhookEventId,
    Guid TenantId,
    int AttemptCount);

public interface IWebhookOutbox
{
    Task<IReadOnlyList<WebhookOutboxItem>> ClaimDueAsync(
        int batchSize,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task ScheduleRetryAsync(
        Guid webhookEventId,
        string reason,
        DateTimeOffset nextAttemptAt,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid webhookEventId,
        string reason,
        CancellationToken cancellationToken = default);
}
