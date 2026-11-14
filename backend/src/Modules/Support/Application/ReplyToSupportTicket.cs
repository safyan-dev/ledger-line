using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Support.Domain;
using PaymentReconciliation.Modules.Tenancy.Contracts;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Support.Application;

public sealed record ReplyToSupportTicketCommand(Guid TicketId, string Body)
    : ICommand<SupportTicketMessageDto>;

public sealed class ReplyToSupportTicketValidator : AbstractValidator<ReplyToSupportTicketCommand>
{
    public ReplyToSupportTicketValidator()
    {
        RuleFor(command => command.Body).NotEmpty().MaximumLength(4000);
    }
}

public sealed class ReplyToSupportTicketHandler(
    IAppDbContext dbContext,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    ITenantAuthorizer authorizer)
    : ICommandHandler<ReplyToSupportTicketCommand, SupportTicketMessageDto>
{
    public async Task<Result<SupportTicketMessageDto>> Handle(
        ReplyToSupportTicketCommand command,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<SupportTicketMessageDto>(Error.Forbidden(
                "tenant.required",
                "A tenant context is required."));
        }

        var authorization = authorizer.Require(TenantPermission.ReplySupportTicket);
        if (authorization.IsFailure)
        {
            return Result.Failure<SupportTicketMessageDto>(authorization.Error!);
        }

        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<SupportTicketMessageDto>(Error.Unauthorized(
                "auth.required",
                "Authentication is required."));
        }

        var ticket = await dbContext.Set<SupportTicket>()
            .FirstOrDefaultAsync(
                candidate => candidate.TenantId == tenantId && candidate.Id == command.TicketId,
                cancellationToken);

        if (ticket is null)
        {
            return Result.Failure<SupportTicketMessageDto>(Error.NotFound(
                "ticket.not_found",
                "The support ticket was not found."));
        }

        var message = ticket.AddMessage(
            userId,
            currentUser.Actor.Name,
            currentUser.Actor.Role,
            command.Body,
            DateTimeOffset.UtcNow,
            currentUser.Actor);

        dbContext.Set<SupportTicketMessage>().Add(message);

        return Result.Success(message.ToDto());
    }
}
