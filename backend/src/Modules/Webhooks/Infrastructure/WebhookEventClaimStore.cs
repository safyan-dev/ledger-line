using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using PaymentReconciliation.Modules.Webhooks.Application;
using PaymentReconciliation.Modules.Webhooks.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Webhooks.Infrastructure;

public sealed class WebhookEventClaimStore(
    IServiceScopeFactory scopeFactory,
    ILogger<WebhookEventClaimStore> logger) : IWebhookEventClaimStore
{
    public async Task<WebhookClaim> ClaimAsync(
        WebhookEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        var payloadHash = WebhookEnvelopeReader.ComputeHash(envelope.RawBody);
        var (providerEventId, eventType) = WebhookEnvelopeReader.ReadIdentifiers(envelope.RawBody);
        var eventId = string.IsNullOrWhiteSpace(providerEventId)
            ? $"invalid_{payloadHash[..32]}"
            : providerEventId.Trim();

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = envelope.CorrelationId,
            ["Provider"] = envelope.Provider,
            ["ProviderEventId"] = eventId
        });

        using var serviceScope = scopeFactory.CreateScope();
        var dbContext = serviceScope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var unitOfWork = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var webhookEvent = WebhookEvent.Create(
            envelope.Provider,
            eventId,
            eventType ?? "unknown",
            envelope.RawBody,
            envelope.Signature,
            envelope.ReceivedAt,
            payloadHash);

        dbContext.Set<WebhookEvent>().Add(webhookEvent);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new WebhookClaim(WebhookClaimKind.Claimed, webhookEvent.Id, null);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            dbContext.ChangeTracker.Clear();

            var existing = await dbContext.Set<WebhookEvent>()
                .FirstOrDefaultAsync(
                    candidate => candidate.Provider == envelope.Provider && candidate.ProviderEventId == eventId,
                    cancellationToken);

            if (existing is null)
            {
                logger.LogWarning(
                    "Webhook {Provider}/{ProviderEventId} collided on insert but could not be reloaded.",
                    envelope.Provider,
                    eventId);
                return new WebhookClaim(WebhookClaimKind.Duplicate, null, "Duplicate event id could not be reloaded.");
            }

            if (string.Equals(existing.PayloadHash, payloadHash, StringComparison.Ordinal))
            {
                if (existing.Status == WebhookStatus.Failed)
                {
                    existing.MarkRetry();
                    await unitOfWork.SaveChangesAsync(cancellationToken);

                    logger.LogInformation(
                        "Retrying failed webhook {Provider}/{ProviderEventId}.",
                        envelope.Provider,
                        eventId);

                    return new WebhookClaim(WebhookClaimKind.Claimed, existing.Id, "Retrying failed event.");
                }

                await dbContext.Set<WebhookEvent>()
                    .Where(candidate => candidate.Id == existing.Id)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(
                            candidate => candidate.DuplicateCount,
                            candidate => candidate.DuplicateCount + 1),
                        cancellationToken);

                logger.LogInformation(
                    "Duplicate webhook {Provider}/{ProviderEventId} discarded.",
                    envelope.Provider,
                    eventId);

                return new WebhookClaim(WebhookClaimKind.Duplicate, existing.Id, "Duplicate payload.");
            }

            existing.RegisterConflict("Same provider event id was received with a different payload.");
            await unitOfWork.DispatchDomainEventsAsync(cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                "Webhook {Provider}/{ProviderEventId} replayed with a different payload; conflict recorded.",
                envelope.Provider,
                eventId);

            return new WebhookClaim(
                WebhookClaimKind.Conflict,
                existing.Id,
                "Same provider event id was received with a different payload.");
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
