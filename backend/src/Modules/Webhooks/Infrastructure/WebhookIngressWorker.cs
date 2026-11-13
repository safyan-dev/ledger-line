using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using PaymentReconciliation.Modules.Webhooks.Application;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Diagnostics;
using PaymentReconciliation.SharedKernel.Results;
using System.Diagnostics;

namespace PaymentReconciliation.Modules.Webhooks.Infrastructure;

public sealed class WebhookIngressWorker(
    IWebhookIngressQueue queue,
    IServiceScopeFactory scopeFactory,
    IOptions<WebhookIngestionOptions> options,
    ILogger<WebhookIngressWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var parallelism = Math.Max(1, options.Value.WorkerCount);
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = parallelism,
            CancellationToken = stoppingToken
        };

        try
        {
            await Parallel.ForEachAsync(
                queue.Reader.ReadAllAsync(stoppingToken),
                parallelOptions,
                async (envelope, cancellationToken) =>
                {
                    await ProcessEnvelopeAsync(envelope, cancellationToken);
                });
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Planned shutdown.
        }
    }

    private async Task ProcessEnvelopeAsync(WebhookEnvelope envelope, CancellationToken cancellationToken)
    {
        WebhookClaim claim;

        try
        {
            using var claimScope = scopeFactory.CreateScope();
            var claimStore = claimScope.ServiceProvider.GetRequiredService<IWebhookEventClaimStore>();
            claim = await claimStore.ClaimAsync(envelope, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to claim webhook {Provider} event; the event was not stored.",
                envelope.Provider);
            LedgerlineMetrics.WebhooksProcessingFailures.Add(1, LedgerlineMetrics.ProviderTag(envelope.Provider));
            return;
        }

        using var activity = LedgerlineMetrics.ActivitySource.StartActivity("webhook.process", ActivityKind.Consumer);
        activity?.SetTag("webhook.provider", envelope.Provider);
        activity?.SetTag("webhook.event_id", claim.WebhookEventId);
        activity?.SetTag("correlation.id", envelope.CorrelationId);

        using var correlationScope = CorrelationContext.Push(envelope.CorrelationId ?? activity?.TraceId.ToString());
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = envelope.CorrelationId ?? activity?.TraceId.ToString(),
            ["WebhookEventId"] = claim.WebhookEventId,
            ["Provider"] = envelope.Provider
        });

        switch (claim.Kind)
        {
            case WebhookClaimKind.Duplicate:
                LedgerlineMetrics.WebhooksDuplicatesIgnored.Add(1, LedgerlineMetrics.ProviderTag(envelope.Provider));
                activity?.SetTag("webhook.outcome", "duplicate");
                logger.LogInformation(
                    "Discarded duplicate webhook {Provider}/{WebhookEventId}.",
                    envelope.Provider,
                    claim.WebhookEventId);
                return;

            case WebhookClaimKind.Conflict:
                activity?.SetTag("webhook.outcome", "conflict");
                logger.LogWarning(
                    "Webhook conflict for {Provider}/{WebhookEventId}: {Reason}",
                    envelope.Provider,
                    claim.WebhookEventId,
                    claim.Reason);
                return;
        }

        if (claim.WebhookEventId is not { } webhookEventId)
        {
            logger.LogError("Webhook claim returned no event id for provider {Provider}.", envelope.Provider);
            activity?.SetStatus(ActivityStatusCode.Error, "Webhook claim returned no event id.");
            return;
        }

        var processingStartedAt = Stopwatch.GetTimestamp();
        logger.LogInformation(
            "Webhook processing started for {Provider}/{WebhookEventId}.",
            envelope.Provider,
            webhookEventId);

        if (envelope.SimulateFailure)
        {
            logger.LogWarning(
                "Simulating a processing failure for webhook {WebhookEventId}.",
                webhookEventId);

            await MarkFailedAsync(envelope.TenantId, webhookEventId, "simulated_processing_failure", cancellationToken);
            RecordProcessingDuration(processingStartedAt, envelope.Provider, "simulated_failure", activity);
            return;
        }

        try
        {
            using var processScope = scopeFactory.CreateScope();
            var tenantContext = processScope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenantContext.Set(new TenantContextSnapshot(envelope.TenantId, string.Empty, null, null, false));
            var dispatcher = processScope.ServiceProvider.GetRequiredService<IDispatcher>();

            var result = await dispatcher.SendAsync(
                new ProcessWebhookEventCommand(webhookEventId),
                cancellationToken);

            if (result.IsFailure)
            {
                logger.LogError(
                    "Webhook {WebhookEventId} processing failed: {ErrorCode} {ErrorMessage}",
                    webhookEventId,
                    result.Error!.Code,
                    result.Error.Message);

                await MarkFailedAsync(envelope.TenantId, webhookEventId, result.Error.Message, cancellationToken);
                RecordProcessingDuration(processingStartedAt, envelope.Provider, "failure", activity);
                activity?.SetStatus(ActivityStatusCode.Error, result.Error.Message);
                return;
            }

            RecordProcessingDuration(processingStartedAt, envelope.Provider, "success", activity);
            logger.LogInformation(
                "Webhook {WebhookEventId} processed in {ElapsedMs:0.0} ms.",
                webhookEventId,
                Stopwatch.GetElapsedTime(processingStartedAt).TotalMilliseconds);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            logger.LogWarning(
                "Concurrent processing for webhook {WebhookEventId} hit a unique constraint.",
                webhookEventId);

            await MarkConflictAsync(
                envelope.TenantId,
                webhookEventId,
                "A concurrent request already created this provider payment.",
                cancellationToken);

            RecordProcessingDuration(processingStartedAt, envelope.Provider, "conflict", activity);
            activity?.SetStatus(ActivityStatusCode.Error, "Concurrent unique constraint.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled error while processing webhook {WebhookEventId}.", webhookEventId);
            await MarkFailedAsync(envelope.TenantId, webhookEventId, "processing_error", cancellationToken);
            RecordProcessingDuration(processingStartedAt, envelope.Provider, "failure", activity);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        }
    }

    private static void RecordProcessingDuration(
        long startedAt,
        string provider,
        string outcome,
        Activity? activity)
    {
        var elapsedSeconds = Stopwatch.GetElapsedTime(startedAt).TotalSeconds;
        LedgerlineMetrics.WebhookProcessingDuration.Record(
            elapsedSeconds,
            LedgerlineMetrics.ProviderTag(provider),
            LedgerlineMetrics.OutcomeTag(outcome));
        activity?.SetTag("webhook.outcome", outcome);
    }

    private async Task MarkFailedAsync(
        Guid tenantId,
        Guid webhookEventId,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>()
                .Set(new TenantContextSnapshot(tenantId, string.Empty, null, null, false));
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            await dispatcher.SendAsync(new FailWebhookEventCommand(webhookEventId, reason), cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not mark webhook {WebhookEventId} as failed.", webhookEventId);
        }
    }

    private async Task MarkConflictAsync(
        Guid tenantId,
        Guid webhookEventId,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>()
                .Set(new TenantContextSnapshot(tenantId, string.Empty, null, null, false));
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            await dispatcher.SendAsync(new MarkWebhookConflictCommand(webhookEventId, reason), cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not mark webhook {WebhookEventId} as a conflict.", webhookEventId);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
