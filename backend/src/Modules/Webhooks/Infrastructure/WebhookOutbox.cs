using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PaymentReconciliation.Modules.Webhooks.Application;
using PaymentReconciliation.Modules.Webhooks.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;

namespace PaymentReconciliation.Modules.Webhooks.Infrastructure;

public sealed class WebhookOutbox(
    IAppDbContext dbContext,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext) : IWebhookOutbox
{
    public async Task<IReadOnlyList<WebhookOutboxItem>> ClaimDueAsync(
        int batchSize,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        // Claim outbox atomically
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE webhooks.webhook_events AS events
            SET status = 'Processing',
                next_attempt_at = NULL
            WHERE events.id IN (
                SELECT id
                FROM webhooks.webhook_events
                WHERE status = 'Received'
                  AND (next_attempt_at IS NULL OR next_attempt_at <= @now)
                ORDER BY received_at
                LIMIT @batch
                FOR UPDATE SKIP LOCKED
            )
            RETURNING events.id, events.tenant_id, events.attempt_count;
            """;
        command.Parameters.AddWithValue("batch", batchSize);
        command.Parameters.AddWithValue("now", now);

        var items = new List<WebhookOutboxItem>(batchSize);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new WebhookOutboxItem(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetInt32(2)));
        }

        return items;
    }

    public async Task ScheduleRetryAsync(
        Guid webhookEventId,
        string reason,
        DateTimeOffset nextAttemptAt,
        CancellationToken cancellationToken = default)
    {
        var webhookEvent = await dbContext.Set<WebhookEvent>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.Id == webhookEventId, cancellationToken);

        if (webhookEvent is null)
        {
            return;
        }

        tenantContext.Set(new TenantContextSnapshot(webhookEvent.TenantId, string.Empty, null, null, false));
        webhookEvent.ScheduleRetry(reason, nextAttemptAt);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
        Guid webhookEventId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var webhookEvent = await dbContext.Set<WebhookEvent>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.Id == webhookEventId, cancellationToken);

        if (webhookEvent is null)
        {
            return;
        }

        tenantContext.Set(new TenantContextSnapshot(webhookEvent.TenantId, string.Empty, null, null, false));
        webhookEvent.MarkFailed(reason);
        await unitOfWork.DispatchDomainEventsAsync(cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
