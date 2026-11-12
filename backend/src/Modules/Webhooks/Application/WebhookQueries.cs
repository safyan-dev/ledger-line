using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Webhooks.Contracts;
using PaymentReconciliation.Modules.Webhooks.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Webhooks.Application;

public sealed record GetWebhookEventsQuery(
    int Page = 1,
    int PageSize = 25,
    string? Status = null,
    string? EventType = null,
    string? Provider = null,
    string? Search = null,
    Guid? PaymentId = null) : IQuery<PagedResult<WebhookEventListItemDto>>;

public sealed class GetWebhookEventsHandler(IAppDbContext dbContext) : IQueryHandler<GetWebhookEventsQuery, PagedResult<WebhookEventListItemDto>>
{
    public async Task<Result<PagedResult<WebhookEventListItemDto>>> Handle(
        GetWebhookEventsQuery query,
        CancellationToken cancellationToken)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var events = dbContext.Set<WebhookEvent>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<WebhookStatus>(query.Status, ignoreCase: true, out var status))
        {
            events = events.Where(webhookEvent => webhookEvent.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            var eventType = query.EventType.Trim();
            events = events.Where(webhookEvent => webhookEvent.EventType == eventType);
        }

        if (!string.IsNullOrWhiteSpace(query.Provider))
        {
            var provider = query.Provider.Trim().ToLowerInvariant();
            events = events.Where(webhookEvent => webhookEvent.Provider == provider);
        }

        if (query.PaymentId is { } paymentId)
        {
            events = events.Where(webhookEvent => webhookEvent.PaymentId == paymentId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            events = events.Where(webhookEvent => webhookEvent.ProviderEventId.ToLower().Contains(search)
                || webhookEvent.EventType.ToLower().Contains(search)
                || (webhookEvent.FailureReason != null && webhookEvent.FailureReason.ToLower().Contains(search)));
        }

        var totalCount = await events.CountAsync(cancellationToken);

        var items = await events
            .OrderByDescending(webhookEvent => webhookEvent.ReceivedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(webhookEvent => new WebhookEventListItemDto(
                webhookEvent.Id,
                webhookEvent.Provider,
                webhookEvent.ProviderEventId,
                webhookEvent.EventType,
                webhookEvent.Status.ToString(),
                webhookEvent.PaymentId,
                webhookEvent.FailureReason,
                webhookEvent.AttemptCount,
                webhookEvent.DuplicateCount,
                webhookEvent.ConflictCount,
                webhookEvent.LastConflictReason,
                webhookEvent.ReceivedAt,
                webhookEvent.ProcessedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(PagedResult<WebhookEventListItemDto>.Create(items, page, pageSize, totalCount));
    }
}

public sealed record GetWebhookEventByIdQuery(Guid WebhookEventId) : IQuery<WebhookEventDto>;

public sealed class GetWebhookEventByIdHandler(IAppDbContext dbContext)
    : IQueryHandler<GetWebhookEventByIdQuery, WebhookEventDto>
{
    public async Task<Result<WebhookEventDto>> Handle(
        GetWebhookEventByIdQuery query,
        CancellationToken cancellationToken)
    {
        var webhookEvent = await dbContext.Set<WebhookEvent>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == query.WebhookEventId, cancellationToken);

        if (webhookEvent is null)
        {
            return Result.Failure<WebhookEventDto>(
                Error.NotFound("webhook.not_found", $"Webhook event {query.WebhookEventId} was not found."));
        }

        return Result.Success(new WebhookEventDto(
            webhookEvent.Id,
            webhookEvent.Provider,
            webhookEvent.ProviderEventId,
            webhookEvent.EventType,
            webhookEvent.Status.ToString(),
            webhookEvent.Payload,
            !string.IsNullOrWhiteSpace(webhookEvent.Signature),
            webhookEvent.PaymentId,
            webhookEvent.FailureReason,
            webhookEvent.AttemptCount,
            webhookEvent.DuplicateCount,
            webhookEvent.ConflictCount,
            webhookEvent.LastConflictReason,
            webhookEvent.ReceivedAt,
            webhookEvent.ProcessedAt));
    }
}
