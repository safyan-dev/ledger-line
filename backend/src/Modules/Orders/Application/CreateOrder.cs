using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Orders.Contracts;
using PaymentReconciliation.Modules.Orders.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Domain;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Orders.Application;

public sealed record CreateOrderCommand(
    string CustomerName,
    string? CustomerReference,
    string? Description,
    decimal ExpectedAmount,
    string Currency,
    string? OrderNumber = null) : ICommand<OrderDto>;

public sealed class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderValidator()
    {
        RuleFor(command => command.CustomerName)
            .NotEmpty()
            .MaximumLength(160);

        RuleFor(command => command.CustomerReference)
            .MaximumLength(64);

        RuleFor(command => command.Description)
            .MaximumLength(400);

        RuleFor(command => command.ExpectedAmount)
            .GreaterThan(0);

        RuleFor(command => command.Currency)
            .NotEmpty()
            .Length(3)
            .Matches("^[A-Za-z]{3}$");

        RuleFor(command => command.OrderNumber)
            .MaximumLength(32);
    }
}

public sealed class CreateOrderHandler(
    IOrderRepository repository,
    IAppDbContext dbContext,
    ICurrentUser currentUser,
    ITenantContext tenantContext)
    : ICommandHandler<CreateOrderCommand, OrderDto>
{
    public async Task<Result<OrderDto>> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var customerReference = string.IsNullOrWhiteSpace(command.CustomerReference)
            ? null
            : command.CustomerReference.Trim();

        if (customerReference is not null)
        {
            var existing = await repository.GetByCustomerReferenceAsync(customerReference, cancellationToken);
            if (existing is not null)
            {
                return Result.Failure<OrderDto>(Error.Conflict(
                    "order.duplicate_reference",
                    $"Customer reference '{customerReference}' is already used by order {existing.OrderNumber}."));
            }
        }

        var requestedOrderNumber = string.IsNullOrWhiteSpace(command.OrderNumber)
            ? null
            : command.OrderNumber.Trim();

        if (requestedOrderNumber is not null)
        {
            var existingOrderNumber = await repository.GetByOrderNumberAsync(requestedOrderNumber, cancellationToken);
            if (existingOrderNumber is not null)
            {
                return Result.Failure<OrderDto>(Error.Conflict(
                    "order.duplicate_number",
                    $"Order number '{requestedOrderNumber}' is already used."));
            }
        }

        var orderNumber = requestedOrderNumber;

        if (orderNumber is null)
        {
            var nextNumber = await dbContext.Database
                .SqlQuery<long>($"SELECT nextval('orders.order_number_seq') AS \"Value\"")
                .SingleAsync(cancellationToken);

            orderNumber = $"ORD-{DateTimeOffset.UtcNow.Year}-{nextNumber:0000}";
        }

        var order = Order.Create(
            orderNumber,
            command.CustomerName,
            customerReference,
            command.Description,
            new Money(command.ExpectedAmount, command.Currency),
            currentUser.Actor,
            tenantId: tenantContext.TenantId);

        repository.Add(order);

        return Result.Success(order.ToDto());
    }
}

internal static class OrderMapping
{
    public static OrderDto ToDto(this Order order) => new(
        order.Id,
        order.OrderNumber,
        order.CustomerName,
        order.CustomerReference,
        order.Description,
        order.ExpectedAmount,
        order.PaidAmount,
        order.Currency,
        order.Status.ToString(),
        order.CreatedAt,
        order.UpdatedAt);

    public static OrderListItemDto ToListItemDto(this Order order) => new(
        order.Id,
        order.OrderNumber,
        order.CustomerName,
        order.CustomerReference,
        order.ExpectedAmount,
        order.PaidAmount,
        order.Currency,
        order.Status.ToString(),
        order.CreatedAt);

    public static OrderCandidate ToCandidate(this Order order) => new(
        order.Id,
        order.OrderNumber,
        order.CustomerName,
        order.CustomerReference,
        order.ExpectedAmount,
        order.PaidAmount,
        order.Currency,
        order.Status.ToString(),
        order.CreatedAt);
}
