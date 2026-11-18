using FluentValidation;
using PaymentReconciliation.Modules.Support.Domain;
using PaymentReconciliation.Modules.Tenancy.Contracts;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Support.Application;

public sealed record CreateSupportTicketCommand(
    string Subject,
    string Description,
    string Category,
    string Priority) : ICommand<SupportTicketDto>;

public sealed class CreateSupportTicketValidator : AbstractValidator<CreateSupportTicketCommand>
{
    public CreateSupportTicketValidator()
    {
        RuleFor(command => command.Subject).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Description).NotEmpty().MaximumLength(4000);
    }
}

public sealed class CreateSupportTicketHandler(
    IAppDbContext dbContext,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    ITenantAuthorizer authorizer,
    IFeatureGate featureGate)
    : ICommandHandler<CreateSupportTicketCommand, SupportTicketDto>
{
    public async Task<Result<SupportTicketDto>> Handle(
        CreateSupportTicketCommand command,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<SupportTicketDto>(Error.Forbidden(
                "tenant.required",
                "A tenant context is required."));
        }

        var authorization = authorizer.Require(TenantPermission.CreateSupportTicket);
        if (authorization.IsFailure)
        {
            return Result.Failure<SupportTicketDto>(authorization.Error!);
        }

        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<SupportTicketDto>(Error.Unauthorized(
                "auth.required",
                "Authentication is required."));
        }

        if (!SupportEnumParsing.TryParse<SupportTicketCategory>(command.Category, out var category))
        {
            return Result.Failure<SupportTicketDto>(Error.Validation(
                "ticket.invalid_category",
                "Category must be TechnicalIssue, PaymentIssue, ReconciliationIssue, AccountIssue, BillingIssue or Other."));
        }

        if (!SupportEnumParsing.TryParse<SupportTicketPriority>(command.Priority, out var priority))
        {
            return Result.Failure<SupportTicketDto>(Error.Validation(
                "ticket.invalid_priority",
                "Priority must be Low, Normal or High."));
        }

        if (priority == SupportTicketPriority.High)
        {
            var feature = await featureGate.EnsureEnabledAsync("priority_support", cancellationToken);
            if (feature.IsFailure)
            {
                return Result.Failure<SupportTicketDto>(feature.Error!);
            }
        }

        var ticket = SupportTicket.Create(
            tenantId,
            command.Subject,
            command.Description,
            category,
            priority,
            userId,
            currentUser.Actor.Name,
            DateTimeOffset.UtcNow,
            currentUser.Actor);

        dbContext.Set<SupportTicket>().Add(ticket);

        return Result.Success(ticket.ToDto());
    }
}
