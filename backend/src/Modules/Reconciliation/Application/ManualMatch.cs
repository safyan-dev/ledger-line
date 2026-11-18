using FluentValidation;
using PaymentReconciliation.Modules.Orders.Contracts;
using PaymentReconciliation.Modules.Payments.Contracts;
using PaymentReconciliation.Modules.Reconciliation.Contracts;
using PaymentReconciliation.Modules.Reconciliation.Domain;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Reconciliation.Application;

public sealed record ManualMatchCommand(Guid PaymentId, Guid OrderId, string? Note)
    : ICommand<ReconciliationRecordDto>;

public sealed class ManualMatchValidator : AbstractValidator<ManualMatchCommand>
{
    public ManualMatchValidator()
    {
        RuleFor(command => command.PaymentId).NotEmpty();
        RuleFor(command => command.OrderId).NotEmpty();
        RuleFor(command => command.Note).MaximumLength(400);
    }
}

public sealed class ManualMatchHandler(
    IPaymentsFacade paymentsFacade,
    IOrdersFacade ordersFacade,
    IReconciliationRepository repository,
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    IFeatureGate featureGate)
    : ICommandHandler<ManualMatchCommand, ReconciliationRecordDto>
{
    public async Task<Result<ReconciliationRecordDto>> Handle(
        ManualMatchCommand command,
        CancellationToken cancellationToken)
    {
        var feature = await featureGate.EnsureEnabledAsync("reconciliation.advanced", cancellationToken);
        if (feature.IsFailure)
        {
            return Result.Failure<ReconciliationRecordDto>(feature.Error!);
        }

        var paymentResult = await paymentsFacade.GetPaymentForUpdateAsync(command.PaymentId, cancellationToken);
        if (paymentResult.IsFailure)
        {
            return Result.Failure<ReconciliationRecordDto>(paymentResult.Error!);
        }

        var payment = paymentResult.Value;

        if (payment.Status == "Matched")
        {
            return Result.Failure<ReconciliationRecordDto>(Error.Conflict(
                "payment.already_matched",
                "This payment is already matched to an order. Reverse the match before linking it again."));
        }

        if (payment.Status is "PartiallyRefunded" or "Refunded")
        {
            return Result.Failure<ReconciliationRecordDto>(Error.Conflict(
                "payment.refunded",
                "A refunded payment cannot be matched to an order."));
        }

        var orderResult = await ordersFacade.GetCandidateAsync(command.OrderId, cancellationToken);
        if (orderResult.IsFailure)
        {
            return Result.Failure<ReconciliationRecordDto>(orderResult.Error!);
        }

        var order = orderResult.Value;
        var actor = currentUser.Actor;

        var allocation = await ordersFacade.AllocatePaymentAsync(
            order.Id,
            payment.Amount,
            payment.Currency,
            payment.Id,
            actor,
            cancellationToken);

        if (allocation.IsFailure)
        {
            return Result.Failure<ReconciliationRecordDto>(allocation.Error!);
        }

        var markMatched = await paymentsFacade.MarkMatchedAsync(payment.Id, order.Id, actor, cancellationToken);
        if (markMatched.IsFailure)
        {
            return Result.Failure<ReconciliationRecordDto>(markMatched.Error!);
        }

        var record = ReconciliationRecord.Create(
            payment.Id,
            order.Id,
            ReconciliationOutcome.ManuallyMatched,
            MatchStrategy.Manual,
            order.ExpectedAmount,
            payment.Amount,
            payment.Currency,
            "Matched manually by an operator.",
            command.Note,
            actor,
            tenantId: tenantContext.TenantId);

        repository.Add(record);

        return Result.Success(record.ToDto(payment.ProviderPaymentId, order.OrderNumber));
    }
}

public sealed record ReRunReconciliationCommand(Guid PaymentId) : ICommand<ReconciliationRecordDto>;

public sealed class ReRunReconciliationHandler(
    IPaymentMatcher matcher,
    IPaymentsFacade paymentsFacade,
    IOrdersFacade ordersFacade,
    IReconciliationRepository repository,
    ICurrentUser currentUser,
    ITenantContext tenantContext)
    : ICommandHandler<ReRunReconciliationCommand, ReconciliationRecordDto>
{
    public async Task<Result<ReconciliationRecordDto>> Handle(
        ReRunReconciliationCommand command,
        CancellationToken cancellationToken)
    {
        var paymentResult = await paymentsFacade.GetPaymentForUpdateAsync(command.PaymentId, cancellationToken);
        if (paymentResult.IsFailure)
        {
            return Result.Failure<ReconciliationRecordDto>(paymentResult.Error!);
        }

        var payment = paymentResult.Value;

        if (payment.Status == "Matched")
        {
            return Result.Failure<ReconciliationRecordDto>(Error.Conflict(
                "reconciliation.already_matched",
                "This payment is already matched. Re-running would allocate it twice."));
        }

        if (payment.Status is "PartiallyRefunded" or "Refunded")
        {
            return Result.Failure<ReconciliationRecordDto>(Error.Conflict(
                "payment.refunded",
                "A refunded payment cannot be reconciled again."));
        }

        var actor = currentUser.Actor;
        var decisionResult = await matcher.MatchAsync(
            new PaymentMatchInput(
                payment.Id,
                payment.OrderId ?? payment.ProviderOrderId,
                payment.Amount,
                payment.Currency,
                payment.Reference,
                payment.CustomerReference,
                payment.ReceivedAt),
            cancellationToken);

        if (decisionResult.IsFailure)
        {
            return Result.Failure<ReconciliationRecordDto>(decisionResult.Error!);
        }

        var decision = decisionResult.Value;
        var autoMatch = decision.OrderId is not null
                        && decision.Outcome is ReconciliationOutcome.Matched or ReconciliationOutcome.PartiallyMatched;

        if (autoMatch && decision.OrderId is { } orderId)
        {
            var allocation = await ordersFacade.AllocatePaymentAsync(
                orderId,
                payment.Amount,
                payment.Currency,
                payment.Id,
                actor,
                cancellationToken);

            if (allocation.IsFailure)
            {
                return Result.Failure<ReconciliationRecordDto>(allocation.Error!);
            }

            var markMatched = await paymentsFacade.MarkMatchedAsync(payment.Id, orderId, actor, cancellationToken);
            if (markMatched.IsFailure)
            {
                return Result.Failure<ReconciliationRecordDto>(markMatched.Error!);
            }
        }
        else
        {
            var markUnmatched = await paymentsFacade.MarkUnmatchedAsync(
                payment.Id,
                decision.Reason ?? "No matching order was found.",
                actor,
                cancellationToken);

            if (markUnmatched.IsFailure)
            {
                return Result.Failure<ReconciliationRecordDto>(markUnmatched.Error!);
            }
        }

        var record = ReconciliationRecord.Create(
            payment.Id,
            decision.OrderId,
            decision.Outcome,
            decision.Strategy,
            decision.ExpectedAmount,
            payment.Amount,
            payment.Currency,
            decision.Reason,
            "Matching re-run by an operator.",
            actor,
            tenantId: tenantContext.TenantId);

        repository.Add(record);

        return Result.Success(record.ToDto(payment.ProviderPaymentId, decision.OrderNumber));
    }
}

internal static class ReconciliationMapping
{
    public static ReconciliationRecordDto ToDto(
        this ReconciliationRecord record,
        string? providerPaymentId,
        string? orderNumber)
        => new(
            record.Id,
            record.PaymentId,
            providerPaymentId,
            record.OrderId,
            orderNumber,
            record.Outcome.ToString(),
            record.Strategy.ToString(),
            record.ExpectedAmount,
            record.ActualAmount,
            record.Difference,
            record.Currency,
            record.Reason,
            record.Notes,
            record.MatchedBy,
            record.CreatedAt);
}
