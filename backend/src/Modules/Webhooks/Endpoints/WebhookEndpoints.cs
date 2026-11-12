using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentReconciliation.Modules.Webhooks.Application;
using PaymentReconciliation.Modules.Webhooks.Contracts;
using PaymentReconciliation.Modules.Webhooks.Infrastructure;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Diagnostics;
using PaymentReconciliation.SharedKernel.Web;

namespace PaymentReconciliation.Modules.Webhooks.Endpoints;

public static class WebhookEndpoints
{
    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/webhooks").WithTags("Webhooks");

        group.MapPost("/{provider}", async (
            string provider,
            HttpRequest request,
            IWebhookSignatureVerifier signatureVerifier,
            IWebhookIngressQueue ingressQueue,
            IOptions<WebhookIngestionOptions> options,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("Ledgerline.Webhooks.Ingress");
            var startedAt = Stopwatch.GetTimestamp();
            var cancellationToken = request.HttpContext.RequestAborted;
            var maxPayloadBytes = options.Value.MaxPayloadBytes;

            if (request.ContentLength is > 0 && request.ContentLength > maxPayloadBytes)
            {
                logger.LogWarning(
                    "Rejected oversized webhook from {Provider}: {ContentLength} bytes.",
                    provider,
                    request.ContentLength);

                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            string rawBody;

            try
            {
                rawBody = await ReadBodyAsync(request, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogDebug(
                    "Webhook body read was cancelled by the client for {Provider}.",
                    provider);

                return Results.StatusCode(StatusCodes.Status408RequestTimeout);
            }
            catch (BadHttpRequestException)
            {
                logger.LogWarning("Webhook body was incomplete for {Provider}.", provider);
                return Results.BadRequest(new { code = "webhook.body_incomplete" });
            }

            if (Encoding.UTF8.GetByteCount(rawBody) > maxPayloadBytes)
            {
                logger.LogWarning("Rejected oversized webhook from {Provider}.", provider);
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            var normalizedProvider = provider.Trim().ToLowerInvariant();
            var signature = request.Headers["X-Provider-Signature"].ToString();
            var signatureResult = signatureVerifier.Verify(
                normalizedProvider,
                rawBody,
                string.IsNullOrWhiteSpace(signature) ? null : signature);

            if (signatureResult.IsFailure)
            {
                logger.LogWarning(
                    "Rejected webhook from {Provider}: {ErrorCode}.",
                    normalizedProvider,
                    signatureResult.Error!.Code);

                return signatureResult.ToProblemResult();
            }

            var envelope = new WebhookEnvelope(
                normalizedProvider,
                rawBody,
                signature,
                DateTimeOffset.UtcNow,
                SimulateFailure: IsSimulationRequested(request, options.Value),
                CorrelationId: request.HttpContext.TraceIdentifier);

            if (!ingressQueue.TryEnqueue(envelope))
            {
                logger.LogWarning("Webhook ingress queue is full; rejecting {Provider}.", normalizedProvider);

                request.HttpContext.Response.Headers.RetryAfter = "1";
                return Results.Problem(
                    title: "Webhook ingress is busy",
                    detail: "The webhook queue is full. Retry with the same event id.",
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    extensions: new Dictionary<string, object?> { ["code"] = "webhook.queue_full" });
            }

            var elapsedMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            var elapsedText = elapsedMs.ToString("0.###", CultureInfo.InvariantCulture);

            LedgerlineMetrics.WebhooksReceived.Add(1, LedgerlineMetrics.ProviderTag(normalizedProvider));

            request.HttpContext.Response.Headers["X-Webhook-Ack-Ms"] = elapsedText;
            request.HttpContext.Response.Headers["Server-Timing"] = $"webhook;dur={elapsedText}";

            if (elapsedMs > options.Value.AckBudgetMs)
            {
                logger.LogWarning(
                    "Webhook ack for {Provider} took {ElapsedMs:0.###} ms, above the {BudgetMs} ms budget.",
                    normalizedProvider,
                    elapsedMs,
                    options.Value.AckBudgetMs);
            }
            else
            {
                logger.LogDebug(
                    "Webhook from {Provider} accepted in {ElapsedMs:0.###} ms.",
                    normalizedProvider,
                    elapsedMs);
            }

            return Results.Accepted(value: new WebhookAck(normalizedProvider, envelope.ReceivedAt, Accepted: true));
        })
        .WithName("IngestWebhook")
        .Produces<WebhookAck>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/", async (
            int? page,
            int? pageSize,
            string? status,
            string? eventType,
            string? provider,
            string? search,
            Guid? paymentId,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new GetWebhookEventsQuery(page ?? 1, pageSize ?? 25, status, eventType, provider, search, paymentId),
                cancellationToken);

            return result.ToHttpResult();
        })
        .WithName("GetWebhookEvents")
        .Produces<PagedResult<WebhookEventListItemDto>>();

        group.MapGet("/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetWebhookEventByIdQuery(id), cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("GetWebhookEventById")
        .Produces<WebhookEventDto>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static bool IsSimulationRequested(HttpRequest request, WebhookIngestionOptions options)
    {
        if (!options.AllowSimulationHeaders)
        {
            return false;
        }

        var header = request.Headers["X-Simulate-Processing-Failure"].ToString();

        return header == "1" || string.Equals(header, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ReadBodyAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);

        return await reader.ReadToEndAsync(cancellationToken);
    }
}
