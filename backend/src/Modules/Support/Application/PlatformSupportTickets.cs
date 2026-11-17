using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Support.Domain;
using PaymentReconciliation.Modules.Tenancy.Contracts;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Support.Application;

public sealed record PlatformSupportTicketDto(
    Guid Id,
    Guid TenantId,
    string TenantName,
    string Subject,
    string Category,
    string Priority,
    string Status,
    string CreatedBy,
    DateTimeOffset CreatedAt);

public sealed record PlatformSupportTicketDetailDto(
    Guid TenantId,
    string TenantName,
    SupportTicketDto Ticket,
    IReadOnlyList<SupportTicketMessageDto> Messages);

public sealed record GetPlatformSupportTicketsQuery(
    int Page = 1,
    int PageSize = 25,
    string? Status = null,
    string? Category = null,
    string? Priority = null,
    string? Search = null) : IQuery<PagedResult<PlatformSupportTicketDto>>;

public sealed class GetPlatformSupportTicketsHandler(
    IAppDbContext dbContext,
    ITenantDirectory tenantDirectory)
    : IQueryHandler<GetPlatformSupportTicketsQuery, PagedResult<PlatformSupportTicketDto>>
{
    public async Task<Result<PagedResult<PlatformSupportTicketDto>>> Handle(
        GetPlatformSupportTicketsQuery query,
        CancellationToken cancellationToken)
    {
        var tickets = dbContext.Set<SupportTicket>()
            .IgnoreQueryFilters()
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!SupportEnumParsing.TryParse<SupportTicketStatus>(query.Status, out var status))
            {
                return Result.Failure<PagedResult<PlatformSupportTicketDto>>(Error.Validation(
                    "ticket.invalid_status",
                    "Ticket status is invalid."));
            }

            tickets = tickets.Where(ticket => ticket.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            if (!SupportEnumParsing.TryParse<SupportTicketCategory>(query.Category, out var category))
            {
                return Result.Failure<PagedResult<PlatformSupportTicketDto>>(Error.Validation(
                    "ticket.invalid_category",
                    "Ticket category is invalid."));
            }

            tickets = tickets.Where(ticket => ticket.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(query.Priority))
        {
            if (!SupportEnumParsing.TryParse<SupportTicketPriority>(query.Priority, out var priority))
            {
                return Result.Failure<PagedResult<PlatformSupportTicketDto>>(Error.Validation(
                    "ticket.invalid_priority",
                    "Ticket priority is invalid."));
            }

            tickets = tickets.Where(ticket => ticket.Priority == priority);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            var tenantIds = await tenantDirectory.FindIdsByNameAsync(search, cancellationToken);
            tickets = tickets.Where(ticket =>
                ticket.Subject.Contains(search)
                || tenantIds.Contains(ticket.TenantId));
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var totalCount = await tickets.CountAsync(cancellationToken);
        var rows = await tickets
            .OrderByDescending(ticket => ticket.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var tenantNames = await tenantDirectory.GetNamesAsync(
            rows.Select(ticket => ticket.TenantId).Distinct().ToArray(),
            cancellationToken);
        var items = rows.Select(ticket => new PlatformSupportTicketDto(
            ticket.Id,
            ticket.TenantId,
            tenantNames[ticket.TenantId],
            ticket.Subject,
            ticket.Category.ToString(),
            ticket.Priority.ToString(),
            ticket.Status.ToString(),
            ticket.CreatedByName,
            ticket.CreatedAt)).ToList();

        return Result.Success(PagedResult<PlatformSupportTicketDto>.Create(items, page, pageSize, totalCount));
    }
}

public sealed record GetPlatformSupportTicketQuery(Guid TicketId)
    : IQuery<PlatformSupportTicketDetailDto>;

public sealed class GetPlatformSupportTicketHandler(
    IAppDbContext dbContext,
    ITenantDirectory tenantDirectory)
    : IQueryHandler<GetPlatformSupportTicketQuery, PlatformSupportTicketDetailDto>
{
    public async Task<Result<PlatformSupportTicketDetailDto>> Handle(
        GetPlatformSupportTicketQuery query,
        CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Set<SupportTicket>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == query.TicketId, cancellationToken);
        if (ticket is null)
        {
            return Result.Failure<PlatformSupportTicketDetailDto>(Error.NotFound(
                "ticket.not_found",
                "The support ticket was not found."));
        }

        var tenantNames = await tenantDirectory.GetNamesAsync([ticket.TenantId], cancellationToken);
        if (!tenantNames.TryGetValue(ticket.TenantId, out var tenantName))
        {
            return Result.Failure<PlatformSupportTicketDetailDto>(Error.NotFound(
                "tenant.not_found",
                "The support ticket's tenant was not found."));
        }
        var messages = await dbContext.Set<SupportTicketMessage>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(message => message.TenantId == ticket.TenantId && message.TicketId == ticket.Id)
            .OrderBy(message => message.CreatedAt)
            .ToListAsync(cancellationToken);

        return Result.Success(new PlatformSupportTicketDetailDto(
            ticket.TenantId,
            tenantName,
            ticket.ToDto(),
            messages.Select(message => message.ToDto()).ToList()));
    }
}

public sealed record ReplyToPlatformSupportTicketCommand(Guid TicketId, string Body)
    : ICommand<SupportTicketMessageDto>;

public sealed class ReplyToPlatformSupportTicketValidator
    : AbstractValidator<ReplyToPlatformSupportTicketCommand>
{
    public ReplyToPlatformSupportTicketValidator()
    {
        RuleFor(command => command.Body).NotEmpty().MaximumLength(4000);
    }
}

public sealed class ReplyToPlatformSupportTicketHandler(
    IAppDbContext dbContext,
    ICurrentUser currentUser,
    IPlatformAuditLogWriter audit)
    : ICommandHandler<ReplyToPlatformSupportTicketCommand, SupportTicketMessageDto>
{
    public async Task<Result<SupportTicketMessageDto>> Handle(
        ReplyToPlatformSupportTicketCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<SupportTicketMessageDto>(Error.Unauthorized(
                "auth.required",
                "Authentication is required."));
        }

        var ticket = await dbContext.Set<SupportTicket>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.Id == command.TicketId, cancellationToken);
        if (ticket is null)
        {
            return Result.Failure<SupportTicketMessageDto>(Error.NotFound(
                "ticket.not_found",
                "The support ticket was not found."));
        }

        var actor = currentUser.Actor with { Role = "PlatformAdmin" };
        var message = ticket.AddMessage(
            userId,
            actor.Name,
            actor.Role,
            command.Body,
            DateTimeOffset.UtcNow,
            actor);
        dbContext.Set<SupportTicketMessage>().Add(message);
        audit.Add(new PlatformAuditRecord(
            "platform.support.replied",
            "SupportTicket",
            ticket.Id,
            ticket.TenantId,
            $"Platform administrator replied to support ticket {ticket.Subject}."));
        return Result.Success(message.ToDto());
    }
}

public sealed record ChangePlatformSupportTicketStatusCommand(Guid TicketId, string Status)
    : ICommand<SupportTicketDto>;

public sealed class ChangePlatformSupportTicketStatusHandler(
    IAppDbContext dbContext,
    ICurrentUser currentUser,
    IPlatformAuditLogWriter audit)
    : ICommandHandler<ChangePlatformSupportTicketStatusCommand, SupportTicketDto>
{
    public async Task<Result<SupportTicketDto>> Handle(
        ChangePlatformSupportTicketStatusCommand command,
        CancellationToken cancellationToken)
    {
        if (!SupportEnumParsing.TryParse<SupportTicketStatus>(command.Status, out var status))
        {
            return Result.Failure<SupportTicketDto>(Error.Validation(
                "ticket.invalid_status",
                "Ticket status is invalid."));
        }

        var ticket = await dbContext.Set<SupportTicket>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.Id == command.TicketId, cancellationToken);
        if (ticket is null)
        {
            return Result.Failure<SupportTicketDto>(Error.NotFound(
                "ticket.not_found",
                "The support ticket was not found."));
        }

        var previousStatus = ticket.Status;
        var actor = currentUser.Actor with { Role = "PlatformAdmin" };
        ticket.ChangeStatus(status, DateTimeOffset.UtcNow, actor);
        audit.Add(new PlatformAuditRecord(
            "platform.support.status_changed",
            "SupportTicket",
            ticket.Id,
            ticket.TenantId,
            $"Support ticket status changed from {previousStatus} to {ticket.Status}.",
            new { previousStatus = previousStatus.ToString(), status = ticket.Status.ToString() }));
        return Result.Success(ticket.ToDto());
    }
}
