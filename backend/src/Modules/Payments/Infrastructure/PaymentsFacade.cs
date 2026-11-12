using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PaymentReconciliation.Modules.Payments.Application;
using PaymentReconciliation.Modules.Payments.Contracts;
using PaymentReconciliation.Modules.Payments.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Diagnostics;
using PaymentReconciliation.SharedKernel.Domain;
using PaymentReconciliation.SharedKernel.Results;
using System.Diagnostics;

namespace PaymentReconciliation.Modules.Payments.Infrastructure;

public sealed class PaymentsFacade(
    IPaymentRepository repository,
    ILogger<PaymentsFacade> logger) : IPaymentsFacade
{
    public async Task<Result<ProviderPaymentApplyResult>> ApplyProviderPaymentAsync(
        ProviderPaymentData payment,
        bool succeeded,
        AuditActor actor,
        CancellationToken cancellationToken = default)
    {
        using var activity = LedgerlineMetrics.ActivitySource.StartActivity("payment.apply", ActivityKind.Internal);
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = CorrelationContext.Current,
            ["Provider"] = payment.Provider,
            ["ProviderPaymentId"] = payment.ProviderPaymentId
        });

        activity?.SetTag("payment.provider", payment.Provider);
        activity?.SetTag("payment.provider_payment_id", payment.ProviderPaymentId);
        if (payment.OrderId is { } taggedOrderId)
        {
            activity?.SetTag("order.id", taggedOrderId);
        }

        logger.LogInformation(
            "Applying provider payment {ProviderPaymentId} from {Provider} (succeeded: {Succeeded}).",
            payment.ProviderPaymentId,
            payment.Provider,
            succeeded);

        var existing = await repository.GetByProviderPaymentIdAsync(
            payment.Provider,
            payment.ProviderPaymentId,
            cancellationToken);

        if (existing is null)
        {
            var created = succeeded
                ? Payment.Create(
                    payment.Provider,
                    payment.ProviderPaymentId,
                    new Money(payment.Amount, payment.Currency),
                    payment.CustomerName,
                    payment.CustomerReference,
                    payment.Reference,
                    payment.ReceivedAt,
                    actor,
                    providerOrderId: payment.OrderId)
                : Payment.CreateFailed(
                    payment.Provider,
                    payment.ProviderPaymentId,
                    new Money(payment.Amount, payment.Currency),
                    payment.CustomerName,
                    payment.CustomerReference,
                    payment.Reference,
                    payment.ReceivedAt,
                    payment.FailureReason ?? "Provider reported a failed payment.",
                    actor,
                    payment.OrderId);

            repository.Add(created);
            activity?.SetTag("payment.id", created.Id);
            activity?.SetTag("payment.status", created.Status.ToString());

            logger.LogInformation(
                "Provider payment {ProviderPaymentId} recorded as {PaymentId} for order {OrderId} with status {Status}.",
                payment.ProviderPaymentId,
                created.Id,
                created.OrderId,
                created.Status);

            return Result.Success(new ProviderPaymentApplyResult(created.ToDto(), IsNew: true, Conflict: false, ConflictReason: null));
        }

        activity?.SetTag("payment.id", existing.Id);

        if (existing.Amount != payment.Amount
            || !string.Equals(existing.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase)
            || (existing.ProviderOrderId is { } providerOrderId && providerOrderId != payment.OrderId)
            || (existing.OrderId is { } linkedOrderId && linkedOrderId != payment.OrderId))
        {
            logger.LogWarning(
                "Provider payment {ProviderPaymentId} conflicts with existing payment {PaymentId}.",
                payment.ProviderPaymentId,
                existing.Id);
            activity?.SetTag("payment.outcome", "conflict");
            activity?.SetStatus(ActivityStatusCode.Error, "Provider payment conflicts with an existing payment.");

            return Result.Success(new ProviderPaymentApplyResult(
                existing.ToDto(),
                IsNew: false,
                Conflict: true,
                ConflictReason: $"Provider payment {payment.ProviderPaymentId} already exists with {existing.Amount:N2} {existing.Currency}; incoming event has {payment.Amount:N2} {payment.Currency}."));
        }

        if (succeeded)
        {
            if (existing.Status == PaymentStatus.Matched)
            {
                logger.LogInformation(
                    "Provider payment {ProviderPaymentId} is already matched as {PaymentId}.",
                    payment.ProviderPaymentId,
                    existing.Id);
                activity?.SetTag("payment.outcome", "already_matched");
                return Result.Success(new ProviderPaymentApplyResult(existing.ToDto(), IsNew: false, Conflict: false, ConflictReason: null));
            }

            if (existing.Status is PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded)
            {
                logger.LogWarning(
                    "Provider payment {ProviderPaymentId} cannot be marked succeeded because {PaymentId} is refunded.",
                    payment.ProviderPaymentId,
                    existing.Id);
                activity?.SetTag("payment.outcome", "refunded_conflict");
                activity?.SetStatus(ActivityStatusCode.Error, "A refunded payment cannot be marked succeeded.");

                return Result.Success(new ProviderPaymentApplyResult(
                    existing.ToDto(),
                    IsNew: false,
                    Conflict: true,
                    ConflictReason: "A refunded payment cannot be marked succeeded."));
            }

            existing.MarkProviderSucceeded(payment.OrderId, actor);
            activity?.SetTag("payment.outcome", "succeeded");
            logger.LogInformation(
                "Provider payment {ProviderPaymentId} marked succeeded as {PaymentId}.",
                payment.ProviderPaymentId,
                existing.Id);

            return Result.Success(new ProviderPaymentApplyResult(existing.ToDto(), IsNew: false, Conflict: false, ConflictReason: null));
        }

        if (existing.Status is PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded)
        {
            logger.LogWarning(
                "Provider payment {ProviderPaymentId} cannot receive a failed event because {PaymentId} is refunded.",
                payment.ProviderPaymentId,
                existing.Id);
            activity?.SetTag("payment.outcome", "refunded_conflict");
            activity?.SetStatus(ActivityStatusCode.Error, "A refunded payment cannot receive a failed event.");

            return Result.Success(new ProviderPaymentApplyResult(
                existing.ToDto(),
                IsNew: false,
                Conflict: true,
                ConflictReason: "A refunded payment cannot receive a failed event."));
        }

        if (existing.Status == PaymentStatus.Matched)
        {
            logger.LogWarning(
                "Provider payment {ProviderPaymentId} cannot be downgraded because {PaymentId} is matched.",
                payment.ProviderPaymentId,
                existing.Id);
            activity?.SetTag("payment.outcome", "matched_conflict");
            activity?.SetStatus(ActivityStatusCode.Error, "Payment is already matched; a failed event cannot downgrade it.");

            return Result.Success(new ProviderPaymentApplyResult(
                existing.ToDto(),
                IsNew: false,
                Conflict: true,
                ConflictReason: "Payment is already matched; a failed event cannot downgrade it."));
        }

        if (existing.Status != PaymentStatus.Failed)
        {
            existing.MarkFailed(payment.FailureReason ?? "Provider reported a failed payment.", actor);
        }

        activity?.SetTag("payment.outcome", "failed");
        logger.LogInformation(
            "Provider payment {ProviderPaymentId} recorded as failed for {PaymentId}.",
            payment.ProviderPaymentId,
            existing.Id);

        return Result.Success(new ProviderPaymentApplyResult(existing.ToDto(), IsNew: false, Conflict: false, ConflictReason: null));
    }

    public async Task<Result<PaymentDto>> GetPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await repository.GetByIdAsync(paymentId, cancellationToken);
        return payment is null
            ? Result.Failure<PaymentDto>(Error.NotFound("payment.not_found", $"Payment {paymentId} was not found."))
            : Result.Success(payment.ToDto());
    }

    public async Task<Result<PaymentDto>> GetPaymentForUpdateAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        await repository.LockForUpdateAsync(paymentId, cancellationToken);

        var payment = await repository.GetByIdAsync(paymentId, cancellationToken);
        return payment is null
            ? Result.Failure<PaymentDto>(Error.NotFound("payment.not_found", $"Payment {paymentId} was not found."))
            : Result.Success(payment.ToDto());
    }

    public async Task<Result<PaymentDto>> MarkMatchedAsync(
        Guid paymentId,
        Guid orderId,
        AuditActor actor,
        CancellationToken cancellationToken = default)
    {
        var payment = await repository.GetByIdAsync(paymentId, cancellationToken);
        if (payment is null)
        {
            return Result.Failure<PaymentDto>(
                Error.NotFound("payment.not_found", $"Payment {paymentId} was not found."));
        }

        payment.LinkToOrder(orderId, actor);
        payment.MarkMatched(orderId, actor);

        return Result.Success(payment.ToDto());
    }

    public async Task<Result<PaymentDto>> MarkUnmatchedAsync(
        Guid paymentId,
        string reason,
        AuditActor actor,
        CancellationToken cancellationToken = default)
    {
        var payment = await repository.GetByIdAsync(paymentId, cancellationToken);
        if (payment is null)
        {
            return Result.Failure<PaymentDto>(
                Error.NotFound("payment.not_found", $"Payment {paymentId} was not found."));
        }

        payment.MarkUnmatched(reason, actor);

        return Result.Success(payment.ToDto());
    }
}
