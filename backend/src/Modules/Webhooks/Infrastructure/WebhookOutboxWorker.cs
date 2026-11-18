using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentReconciliation.Modules.Webhooks.Application;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;

namespace PaymentReconciliation.Modules.Webhooks.Infrastructure;

public sealed class WebhookOutboxWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<WebhookIngestionOptions> options,
    ILogger<WebhookOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromMilliseconds(Math.Max(100, options.Value.OutboxPollMilliseconds)));

        try
        {
            do
            {
                await ProcessBatchAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Planned shutdown.
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var claimScope = scopeFactory.CreateScope();
            var outbox = claimScope.ServiceProvider.GetRequiredService<IWebhookOutbox>();
            var items = await outbox.ClaimDueAsync(
                Math.Max(1, options.Value.OutboxBatchSize),
                DateTimeOffset.UtcNow,
                cancellationToken);

            foreach (var item in items)
            {
                await ProcessItemAsync(item, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Planned shutdown.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Webhook outbox polling failed.");
        }
    }

    private async Task ProcessItemAsync(
        WebhookOutboxItem item,
        CancellationToken cancellationToken)
    {
        try
        {
            using var processingScope = scopeFactory.CreateScope();
            processingScope.ServiceProvider.GetRequiredService<ITenantContext>()
                .Set(new TenantContextSnapshot(item.TenantId, string.Empty, null, null, false));
            var dispatcher = processingScope.ServiceProvider.GetRequiredService<IDispatcher>();

            var result = await dispatcher.SendAsync(
                new ProcessWebhookEventCommand(item.WebhookEventId),
                cancellationToken);

            if (result.IsFailure)
            {
                await ScheduleRetryAsync(item, result.Error!.Message, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Webhook outbox item {WebhookEventId} failed.",
                item.WebhookEventId);
            await ScheduleRetryAsync(item, "processing_error", cancellationToken);
        }
    }

    private async Task ScheduleRetryAsync(
        WebhookOutboxItem item,
        string reason,
        CancellationToken cancellationToken)
    {
        using var retryScope = scopeFactory.CreateScope();
        var outbox = retryScope.ServiceProvider.GetRequiredService<IWebhookOutbox>();

        if (item.AttemptCount >= Math.Max(1, options.Value.MaxAttempts))
        {
            await outbox.MarkFailedAsync(item.WebhookEventId, reason, cancellationToken);
            return;
        }

        var exponent = Math.Min(10, Math.Max(0, item.AttemptCount - 1));
        var delaySeconds = Math.Min(
            3600,
            Math.Max(1, options.Value.RetryBaseSeconds) * Math.Pow(2, exponent));
        var nextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(delaySeconds);

        await outbox.ScheduleRetryAsync(
            item.WebhookEventId,
            reason,
            nextAttemptAt,
            cancellationToken);
    }
}
