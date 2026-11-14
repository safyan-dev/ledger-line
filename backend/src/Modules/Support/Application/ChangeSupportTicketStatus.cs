using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Support.Domain;
using PaymentReconciliation.Modules.Tenancy.Contracts;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Support.Application;

public sealed record ChangeSupportTicketStatusCommand(Guid TicketId, string Status)
    : ICommand<SupportTicketDto>;

public sealed class ChangeSupportTicketStatusHandler(
    IAppDbContext dbContext,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    ITenantAuthorizer authorizer)
    : ICommandHandler<ChangeSupportTicketStatusCommand, SupportTicketDto>
{
    public async Task<Result<SupportTicketDto>> Handle(
        ChangeSupportTicketStatusCommand command,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<SupportTicketDto>(Error.Forbidden(
                "tenant.required",
                "A tenant context is required."));
        }

        var authorization = authorizer.Require(TenantPermission.ResolveSupportTicket);
        if (authorization.IsFailure)
        {
            return Result.Failure<SupportTicketDto>(authorization.Error!);
        }

        if (!SupportEnumParsing.TryParse<SupportTicketStatus>(command.Status, out var status))
        {
            return Result.Failure<SupportTicketDto>(Error.Validation(
                "ticket.invalid_status",
                "Status must be Open, InProgress, WaitingForCustomer, Resolved or Closed."));
        }

        var ticket = await dbContext.Set<SupportTicket>()
            .FirstOrDefaultAsync(
                candidate => candidate.TenantId == tenantId && candidate.Id == command.TicketId,
                cancellationToken);

        if (ticket is null)
        {
            return Result.Failure<SupportTicketDto>(Error.NotFound(
                "ticket.not_found",
                "The support ticket was not found."));
        }

        ticket.ChangeStatus(status, DateTimeOffset.UtcNow, currentUser.Actor);

        return Result.Success(ticket.ToDto());
    }
}
