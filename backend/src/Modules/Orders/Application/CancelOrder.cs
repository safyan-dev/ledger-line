using FluentValidation;
using PaymentReconciliation.Modules.Orders.Contracts;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Domain;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Orders.Application;

public sealed record CancelOrderCommand(Guid OrderId, string Reason) : ICommand<OrderDto>;

public sealed class CancelOrderValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderValidator()
    {
        RuleFor(command => command.OrderId).NotEmpty();
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(400);
    }
}

public sealed class CancelOrderHandler(IOrderRepository repository, ICurrentUser currentUser)
    : ICommandHandler<CancelOrderCommand, OrderDto>
{
    public async Task<Result<OrderDto>> Handle(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await repository.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure<OrderDto>(
                Error.NotFound("order.not_found", $"Order {command.OrderId} was not found."));
        }

        order.Cancel(command.Reason, currentUser.Actor);

        return Result.Success(order.ToDto());
    }
}
