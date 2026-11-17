using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Auditing.Contracts;
using PaymentReconciliation.Modules.Auditing.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Results;


namespace PaymentReconciliation.Modules.Auditing.Application;

public sealed record GetPlatformAuditLogsQuery(
    int Page = 1,
    int PageSize = 25,
    string? Action = null,
    string? TargetType = null,
    Guid? TargetId = null,
    Guid? TenantId = null,
    string? Search = null) : IQuery<PagedResult<PlatformAuditLogDto>>;

public sealed class GetPlatformAuditLogsHandler(IAppDbContext dbContext)
    : IQueryHandler<GetPlatformAuditLogsQuery, PagedResult<PlatformAuditLogDto>>
{
    public async Task<Result<PagedResult<PlatformAuditLogDto>>> Handle(
        GetPlatformAuditLogsQuery query,
        CancellationToken cancellationToken)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var logs = dbContext.Set<PlatformAuditLog>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim().ToLowerInvariant();
            logs = logs.Where(log => log.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(query.TargetType))
        {
            var targetType = query.TargetType.Trim();
            logs = logs.Where(log => log.TargetType == targetType);
        }

        if (query.TargetId is { } targetId)
        {
            logs = logs.Where(log => log.TargetId == targetId);
        }

        if (query.TenantId is { } tenantId)
        {
            logs = logs.Where(log => log.TenantId == tenantId);
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
            .Select(log => new PlatformAuditLogDto(
                log.Id,
                log.ActorId,
                log.ActorName,
                log.ActorEmail,
                log.ActorRole,
                log.Action,
                log.TargetType,
                log.TargetId,
                log.TenantId,
                log.Summary,
                log.Metadata,
                log.CorrelationId,
                log.OccurredAt))
            .ToListAsync(cancellationToken);

        return Result.Success(PagedResult<PlatformAuditLogDto>.Create(items, page, pageSize, totalCount));
    }
}
