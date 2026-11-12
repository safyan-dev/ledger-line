using Microsoft.Extensions.Logging;
using PaymentReconciliation.Modules.Orders.Contracts;
using PaymentReconciliation.Modules.Payments.Contracts;
using PaymentReconciliation.Modules.Payments.Contracts.Events;
using PaymentReconciliation.Modules.Reconciliation.Contracts.Events;
using PaymentReconciliation.Modules.Reconciliation.Domain;
using PaymentReconciliation.SharedKernel.Diagnostics;
using PaymentReconciliation.SharedKernel.Events;
using PaymentReconciliation.SharedKernel.Results;
using System.Diagnostics;

namespace PaymentReconciliation.Modules.Reconciliation.Application;

public sealed class ReconcilePaymentHandler(
    IPaymentMatcher matcher,
    IOrdersFacade ordersFacade,
    IPaymentsFacade paymentsFacade,
    IReconciliationRepository repository,
    ILogger<ReconcilePaymentHandler> logger)
    : IDomainEventHandler<PaymentRecorded>
{
    public async Task<Result> HandleAsync(PaymentRecorded domainEvent, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcomeLabel = "Error";

        using var activity = LedgerlineMetrics.ActivitySource.StartActivity(
            "reconciliation.process",
            ActivityKind.Internal);
        activity?.SetTag("payment.id", domainEvent.PaymentId);
        activity?.SetTag("payment.provider", domainEvent.Provider);
        if (domainEvent.OrderId is { } eventOrderId)
        {
            activity?.SetTag("order.id", eventOrderId);
        }

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = CorrelationContext.Current ?? activity?.TraceId.ToString(),
            ["PaymentId"] = domainEvent.PaymentId,
            ["OrderId"] = domainEvent.OrderId
        });

        logger.LogInformation(
            "Reconciliation started for payment {PaymentId} from {Provider}.",
            domainEvent.PaymentId,
            domainEvent.Provider);

        try
        {
            var input = new PaymentMatchInput(
                domainEvent.PaymentId,
                domainEvent.OrderId,
                domainEvent.Amount,
                domainEvent.Currency,
                domainEvent.Reference,
                domainEvent.CustomerReference,
                domainEvent.ReceivedAt);

            var decisionResult = await matcher.MatchAsync(input, cancellationToken);
            if (decisionResult.IsFailure)
            {
                logger.LogError(
                    "Reconciliation failed for payment {PaymentId}: {ErrorCode} {ErrorMessage}",
                    domainEvent.PaymentId,
                    decisionResult.Error!.Code,
                    decisionResult.Error.Message);
                return Result.Failure(decisionResult.Error!);
            }

            var decision = decisionResult.Value;
            outcomeLabel = decision.Outcome.ToString();
            activity?.SetTag("reconciliation.outcome", outcomeLabel);
            activity?.SetTag("reconciliation.strategy", decision.Strategy.ToString());
            if (decision.OrderId is { } matchedOrderId)
            {
                activity?.SetTag("order.id", matchedOrderId);
            }

            var actor = domainEvent.Actor;
            var autoMatch = decision.OrderId is not null
                            && decision.Outcome is ReconciliationOutcome.Matched or ReconciliationOutcome.PartiallyMatched;

            if (autoMatch && decision.OrderId is { } orderId)
            {
                var allocation = await ordersFacade.AllocatePaymentAsync(
                    orderId,
                    domainEvent.Amount,
                    domainEvent.Currency,
                    domainEvent.PaymentId,
                    actor,
                    cancellationToken);

                if (allocation.IsFailure)
                {
                    outcomeLabel = "Error";
                    logger.LogError(
                        "Reconciliation allocation failed for payment {PaymentId} and order {OrderId}: {ErrorCode} {ErrorMessage}",
                        domainEvent.PaymentId,
                        orderId,
                        allocation.Error!.Code,
                        allocation.Error.Message);
                    return Result.Failure(allocation.Error!);
                }

                var markMatched = await paymentsFacade.MarkMatchedAsync(
                    domainEvent.PaymentId,
                    orderId,
                    actor,
                    cancellationToken);

                if (markMatched.IsFailure)
                {
                    outcomeLabel = "Error";
                    logger.LogError(
                        "Could not mark payment {PaymentId} as matched to order {OrderId}: {ErrorCode} {ErrorMessage}",
                        domainEvent.PaymentId,
                        orderId,
                        markMatched.Error!.Code,
                        markMatched.Error.Message);
                    return Result.Failure(markMatched.Error!);
                }
            }
            else
            {
                var markUnmatched = await paymentsFacade.MarkUnmatchedAsync(
                    domainEvent.PaymentId,
                    decision.Reason ?? "No matching order was found.",
                    actor,
                    cancellationToken);

                if (markUnmatched.IsFailure)
                {
                    outcomeLabel = "Error";
                    logger.LogError(
                        "Could not mark payment {PaymentId} as unmatched: {ErrorCode} {ErrorMessage}",
                        domainEvent.PaymentId,
                        markUnmatched.Error!.Code,
                        markUnmatched.Error.Message);
                    return Result.Failure(markUnmatched.Error!);
                }
            }

            var record = ReconciliationRecord.Create(
                domainEvent.PaymentId,
                decision.OrderId,
                decision.Outcome,
                decision.Strategy,
                decision.ExpectedAmount,
                domainEvent.Amount,
                domainEvent.Currency,
                decision.Reason,
                notes: null,
                actor);

            repository.Add(record);

            logger.LogInformation(
                "Reconciliation completed for payment {PaymentId} with outcome {Outcome} via {Strategy}.",
                domainEvent.PaymentId,
                decision.Outcome,
                decision.Strategy);

            return Result.Success();
        }
        finally
        {
            var elapsedSeconds = Stopwatch.GetElapsedTime(startedAt).TotalSeconds;
            LedgerlineMetrics.ReconciliationProcessingDuration.Record(
                elapsedSeconds,
                LedgerlineMetrics.OutcomeTag(outcomeLabel));

            if (outcomeLabel == "Error")
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Reconciliation failed.");
            }
        }
    }
}
