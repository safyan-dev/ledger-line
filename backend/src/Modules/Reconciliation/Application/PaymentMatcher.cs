using PaymentReconciliation.Modules.Orders.Contracts;
using PaymentReconciliation.Modules.Reconciliation.Domain;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Reconciliation.Application;

public sealed record PaymentMatchInput(
    Guid PaymentId,
    Guid? OrderId,
    decimal Amount,
    string Currency,
    string? Reference,
    string? CustomerReference,
    DateTimeOffset ReceivedAt);

public sealed record MatchDecision(
    MatchStrategy Strategy,
    ReconciliationOutcome Outcome,
    Guid? OrderId,
    string? OrderNumber,
    decimal? ExpectedAmount,
    int CandidateCount,
    string? Reason);

public interface IPaymentMatcher
{
    Task<Result<MatchDecision>> MatchAsync(PaymentMatchInput payment, CancellationToken cancellationToken = default);
}

public sealed class PaymentMatcher(IOrdersFacade ordersFacade) : IPaymentMatcher
{
    public async Task<Result<MatchDecision>> MatchAsync(
        PaymentMatchInput payment,
        CancellationToken cancellationToken = default)
    {
        string? directOrderFailure = null;

        if (payment.OrderId is { } orderId)
        {
            var byOrderId = await ordersFacade.GetCandidateAsync(orderId, cancellationToken);
            if (byOrderId.IsSuccess)
            {
                var decision = Decide(
                    [byOrderId.Value],
                    payment,
                    MatchStrategy.OrderId,
                    "Provider order id matched.");

                return Result.Success(decision!);
            }

            directOrderFailure = byOrderId.Error!.Message;
        }

        if (!string.IsNullOrWhiteSpace(payment.Reference))
        {
            var byReference = await ordersFacade.FindCandidatesByOrderNumberAsync(payment.Reference, cancellationToken);
            if (byReference.IsFailure)
            {
                return Result.Failure<MatchDecision>(byReference.Error!);
            }

            var decision = Decide(byReference.Value, payment, MatchStrategy.ExactReference, "Order number reference matched.");
            if (decision is not null)
            {
                return Result.Success(decision);
            }
        }

        if (!string.IsNullOrWhiteSpace(payment.CustomerReference))
        {
            var byCustomer = await ordersFacade.FindCandidatesByCustomerAndAmountAsync(
                payment.CustomerReference,
                payment.Amount,
                payment.Currency,
                cancellationToken);

            if (byCustomer.IsFailure)
            {
                return Result.Failure<MatchDecision>(byCustomer.Error!);
            }

            var decision = Decide(byCustomer.Value, payment, MatchStrategy.CustomerAndAmount, "Customer reference and amount matched.");
            if (decision is not null)
            {
                return Result.Success(decision);
            }
        }

        var hasIdentifier = payment.OrderId is not null
                            || !string.IsNullOrWhiteSpace(payment.Reference)
                            || !string.IsNullOrWhiteSpace(payment.CustomerReference);

        if (!hasIdentifier)
        {
            var byAmount = await ordersFacade.FindCandidatesByAmountAndDateAsync(
                payment.Amount,
                payment.Currency,
                payment.ReceivedAt,
                cancellationToken);

            if (byAmount.IsFailure)
            {
                return Result.Failure<MatchDecision>(byAmount.Error!);
            }

            var amountDecision = Decide(byAmount.Value, payment, MatchStrategy.AmountAndDate, "Amount matched an order created within three days.");
            if (amountDecision is not null)
            {
                return Result.Success(amountDecision);
            }
        }

        return Result.Success(new MatchDecision(
            MatchStrategy.None,
            ReconciliationOutcome.Unmatched,
            OrderId: null,
            OrderNumber: null,
            ExpectedAmount: null,
            CandidateCount: 0,
            Reason: directOrderFailure ?? "No matching order was found for the supplied identifiers."));
    }

    private static MatchDecision? Decide(
        IReadOnlyList<OrderCandidate> candidates,
        PaymentMatchInput payment,
        MatchStrategy strategy,
        string reason)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        if (candidates.Count > 1)
        {
            return new MatchDecision(
                strategy,
                ReconciliationOutcome.Ambiguous,
                OrderId: null,
                OrderNumber: null,
                ExpectedAmount: null,
                candidates.Count,
                $"Found {candidates.Count} candidate orders. Manual review required.");
        }

        var order = candidates[0];

        if (!string.Equals(order.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return new MatchDecision(
                strategy,
                ReconciliationOutcome.Unmatched,
                order.Id,
                order.OrderNumber,
                ExpectedAmount: null,
                candidates.Count,
                $"Currency mismatch: payment is {payment.Currency}, order {order.OrderNumber} is {order.Currency}. Manual review required.");
        }

        var totalPaid = decimal.Round(order.PaidAmount + payment.Amount, 2, MidpointRounding.AwayFromZero);

        if (payment.Amount != order.ExpectedAmount)
        {
            return new MatchDecision(
                strategy,
                ReconciliationOutcome.AmountMismatch,
                order.Id,
                order.OrderNumber,
                order.ExpectedAmount,
                candidates.Count,
                $"Amount mismatch: payment {payment.Amount:N2} {payment.Currency}, order {order.OrderNumber} expects {order.ExpectedAmount:N2} {order.Currency}.");
        }

        var outcome = totalPaid > order.ExpectedAmount
            ? ReconciliationOutcome.Overpaid
            : totalPaid == order.ExpectedAmount
                ? ReconciliationOutcome.Matched
                : ReconciliationOutcome.PartiallyMatched;

        var decisionReason = outcome == ReconciliationOutcome.Overpaid
            ? $"Payment would overpay order {order.OrderNumber} (expected {order.ExpectedAmount:N2}, already paid {order.PaidAmount:N2}). Manual review required."
            : reason;

        return new MatchDecision(
            strategy,
            outcome,
            order.Id,
            order.OrderNumber,
            order.ExpectedAmount,
            candidates.Count,
            decisionReason);
    }
}
