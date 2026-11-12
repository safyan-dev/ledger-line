using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Auditing.Contracts;
using PaymentReconciliation.Modules.Auditing.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Auditing.Application;

public sealed record GetAuditLogsQuery(
    int Page = 1,
    int PageSize = 25,
    string? Module = null,
    string? Action = null,
    string? EntityType = null,
    Guid? EntityId = null,
    string? Search = null) : IQuery<PagedResult<AuditLogDto>>;

public sealed class GetAuditLogsHandler(IAppDbContext dbContext)
    : IQueryHandler<GetAuditLogsQuery, PagedResult<AuditLogDto>>
{
    public async Task<Result<PagedResult<AuditLogDto>>> Handle(
        GetAuditLogsQuery query,
        CancellationToken cancellationToken)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var logs = dbContext.Set<AuditLog>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Module))
        {
            var module = query.Module.Trim().ToLowerInvariant();
            logs = logs.Where(log => log.Module == module);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim().ToLowerInvariant();
            logs = logs.Where(log => log.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            var entityType = query.EntityType.Trim();
            logs = logs.Where(log => log.EntityType == entityType);
        }

        if (query.EntityId is { } entityId)
        {
            logs = logs.Where(log => log.EntityId == entityId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            logs = logs.Where(log =>
                log.Summary.ToLower().Contains(search)
                || log.ActorName.ToLower().Contains(search)
                || log.Action.ToLower().Contains(search));
        }

        var totalCount = await logs.CountAsync(cancellationToken);

        var items = await logs
            .OrderByDescending(log => log.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(log => new AuditLogDto(
                log.Id,
                log.ActorId,
                log.ActorName,
                log.ActorRole,
                log.Action,
                log.Module,
                log.EntityType,
                log.EntityId,
                log.Summary,
                log.Metadata,
                log.CorrelationId,
                log.OccurredAt))
            .ToListAsync(cancellationToken);

        return Result.Success(PagedResult<AuditLogDto>.Create(items, page, pageSize, totalCount));
    }
}
