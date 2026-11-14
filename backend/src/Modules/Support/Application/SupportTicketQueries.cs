using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Support.Domain;
using PaymentReconciliation.Modules.Tenancy.Contracts;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Support.Application;

public sealed record GetSupportTicketsQuery(
    int Page = 1,
    int PageSize = 25,
    string? Status = null,
    string? Category = null,
    string? Priority = null) : IQuery<PagedResult<SupportTicketDto>>;

public sealed class GetSupportTicketsHandler(
    IAppDbContext dbContext,
    ITenantContext tenantContext,
    ITenantAuthorizer authorizer)
    : IQueryHandler<GetSupportTicketsQuery, PagedResult<SupportTicketDto>>
{
    public async Task<Result<PagedResult<SupportTicketDto>>> Handle(
        GetSupportTicketsQuery query,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<PagedResult<SupportTicketDto>>(Error.Forbidden(
                "tenant.required",
                "A tenant context is required."));
        }

        var authorization = authorizer.Require(TenantPermission.ViewSupportTicket);
        if (authorization.IsFailure)
        {
            return Result.Failure<PagedResult<SupportTicketDto>>(authorization.Error!);
        }

        var tickets = dbContext.Set<SupportTicket>()
            .AsNoTracking()
            .Where(ticket => ticket.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!SupportEnumParsing.TryParse<SupportTicketStatus>(query.Status, out var status))
            {
                return Result.Failure<PagedResult<SupportTicketDto>>(Error.Validation(
                    "ticket.invalid_status",
                    "Status must be Open, InProgress, WaitingForCustomer, Resolved or Closed."));
            }

            tickets = tickets.Where(ticket => ticket.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            if (!SupportEnumParsing.TryParse<SupportTicketCategory>(query.Category, out var category))
            {
                return Result.Failure<PagedResult<SupportTicketDto>>(Error.Validation(
                    "ticket.invalid_category",
                    "Category is invalid."));
            }

            tickets = tickets.Where(ticket => ticket.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(query.Priority))
        {
            if (!SupportEnumParsing.TryParse<SupportTicketPriority>(query.Priority, out var priority))
            {
                return Result.Failure<PagedResult<SupportTicketDto>>(Error.Validation(
                    "ticket.invalid_priority",
                    "Priority is invalid."));
            }

            tickets = tickets.Where(ticket => ticket.Priority == priority);
        }

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var totalCount = await tickets.CountAsync(cancellationToken);

        var rows = await tickets
            .OrderByDescending(ticket => ticket.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(ticket => ticket.ToDto()).ToList();
        return Result.Success(PagedResult<SupportTicketDto>.Create(items, page, pageSize, totalCount));
    }
}

public sealed record GetSupportTicketQuery(Guid TicketId) : IQuery<SupportTicketDetailDto>;

public sealed class GetSupportTicketHandler(
    IAppDbContext dbContext,
    ITenantContext tenantContext,
    ITenantAuthorizer authorizer)
    : IQueryHandler<GetSupportTicketQuery, SupportTicketDetailDto>
{
    public async Task<Result<SupportTicketDetailDto>> Handle(
        GetSupportTicketQuery query,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<SupportTicketDetailDto>(Error.Forbidden(
                "tenant.required",
                "A tenant context is required."));
        }

        var authorization = authorizer.Require(TenantPermission.ViewSupportTicket);
        if (authorization.IsFailure)
        {
            return Result.Failure<SupportTicketDetailDto>(authorization.Error!);
        }

        var ticket = await dbContext.Set<SupportTicket>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.TenantId == tenantId && candidate.Id == query.TicketId,
                cancellationToken);

        if (ticket is null)
        {
            return Result.Failure<SupportTicketDetailDto>(Error.NotFound(
                "ticket.not_found",
                "The support ticket was not found."));
        }

        var messages = await dbContext.Set<SupportTicketMessage>()
            .AsNoTracking()
            .Where(message => message.TenantId == tenantId && message.TicketId == ticket.Id)
            .OrderBy(message => message.CreatedAt)
            .ToListAsync(cancellationToken);

        return Result.Success(new SupportTicketDetailDto(
            ticket.ToDto(),
            messages.Select(message => message.ToDto()).ToList()));
    }
}
