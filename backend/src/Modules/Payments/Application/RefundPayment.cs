using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Orders.Contracts;
using PaymentReconciliation.Modules.Payments.Contracts;
using PaymentReconciliation.Modules.Payments.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Payments.Application;

public sealed record RefundPaymentCommand(Guid PaymentId, decimal Amount, string Reason)
    : ICommand<RefundDto>;

public sealed class RefundPaymentValidator : AbstractValidator<RefundPaymentCommand>
{
    public RefundPaymentValidator()
    {
        RuleFor(command => command.PaymentId).NotEmpty();
        RuleFor(command => command.Amount).GreaterThan(0);
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(400);
    }
}

public sealed class RefundPaymentHandler(
    IPaymentRepository paymentRepository,
    IRefundRepository refundRepository,
    IOrdersFacade ordersFacade,
    ICurrentUser currentUser)
    : ICommandHandler<RefundPaymentCommand, RefundDto>
{
    public async Task<Result<RefundDto>> Handle(
        RefundPaymentCommand command,
        CancellationToken cancellationToken)
    {
        await paymentRepository.LockForUpdateAsync(command.PaymentId, cancellationToken);

        var payment = await paymentRepository.GetByIdAsync(command.PaymentId, cancellationToken);
        if (payment is null)
        {
            return Result.Failure<RefundDto>(
                Error.NotFound("payment.not_found", $"Payment {command.PaymentId} was not found."));
        }

        if (payment.Status == PaymentStatus.Refunded)
        {
            return Result.Failure<RefundDto>(
                Error.Conflict("refund.already_refunded", "This payment has already been fully refunded."));
        }

        if (payment.Status == PaymentStatus.Failed)
        {
            return Result.Failure<RefundDto>(
                Error.Conflict("payment.not_refundable", "A failed payment cannot be refunded."));
        }

        if (command.Amount > payment.RefundableAmount)
        {
            return Result.Failure<RefundDto>(Error.Conflict(
                "refund.exceeds_available",
                $"Refund exceeds the refundable amount of {payment.RefundableAmount:N2} {payment.Currency}."));
        }

        var actor = currentUser.Actor;

        payment.RecordRefund(command.Amount);

        if (payment.OrderId is { } orderId)
        {
            var reversal = await ordersFacade.ReversePaymentAllocationAsync(
                orderId,
                payment.Id,
                command.Amount,
                payment.Currency,
                actor,
                cancellationToken);

            if (reversal.IsFailure)
            {
                return Result.Failure<RefundDto>(reversal.Error!);
            }
        }

        var refund = Refund.Create(payment, command.Amount, command.Reason, actor);
        refundRepository.Add(refund);

        return Result.Success(refund.ToDto());
    }
}

public sealed record GetRefundsForPaymentQuery(Guid PaymentId) : IQuery<IReadOnlyList<RefundDto>>;

public sealed class GetRefundsForPaymentHandler(IAppDbContext dbContext)
    : IQueryHandler<GetRefundsForPaymentQuery, IReadOnlyList<RefundDto>>
{
    public async Task<Result<IReadOnlyList<RefundDto>>> Handle(
        GetRefundsForPaymentQuery query,
        CancellationToken cancellationToken)
    {
        var refunds = await dbContext.Set<Refund>()
            .AsNoTracking()
            .Where(refund => refund.PaymentId == query.PaymentId)
            .OrderByDescending(refund => refund.CreatedAt)
            .Select(refund => new RefundDto(
                refund.Id,
                refund.PaymentId,
                refund.OrderId,
                refund.Amount,
                refund.Currency,
                refund.Reason,
                refund.Status.ToString(),
                refund.Provider,
                refund.ProviderRefundId,
                refund.CreatedBy,
                refund.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<RefundDto>>(refunds);
    }
}
