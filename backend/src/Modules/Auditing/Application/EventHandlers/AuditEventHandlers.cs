using PaymentReconciliation.Modules.Auditing.Domain;
using PaymentReconciliation.Modules.Orders.Contracts.Events;
using PaymentReconciliation.Modules.Payments.Contracts.Events;
using PaymentReconciliation.Modules.Reconciliation.Contracts.Events;
using PaymentReconciliation.Modules.Webhooks.Contracts.Events;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Events;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Auditing.Application.EventHandlers;

public sealed class OrderCreatedAuditHandler(IAuditLogRepository repository) : IDomainEventHandler<OrderCreated>
{
    public Task<Result> HandleAsync(OrderCreated domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            domainEvent.Actor,
            "order.created",
            "orders",
            "Order",
            domainEvent.OrderId,
            $"Order {domainEvent.OrderNumber} created for {domainEvent.CustomerName} ({domainEvent.Currency} {domainEvent.ExpectedAmount:N2}).",
            new { domainEvent.OrderNumber, domainEvent.ExpectedAmount, domainEvent.Currency }));

        return Task.FromResult(Result.Success());
    }
}

public sealed class OrderCancelledAuditHandler(IAuditLogRepository repository) : IDomainEventHandler<OrderCancelled>
{
    public Task<Result> HandleAsync(OrderCancelled domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            domainEvent.Actor,
            "order.cancelled",
            "orders",
            "Order",
            domainEvent.OrderId,
            $"Order {domainEvent.OrderNumber} cancelled. Reason: {domainEvent.Reason}.",
            new { domainEvent.Reason }));

        return Task.FromResult(Result.Success());
    }
}

public sealed class PaymentRecordedAuditHandler(IAuditLogRepository repository) : IDomainEventHandler<PaymentRecorded>
{
    public Task<Result> HandleAsync(PaymentRecorded domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            domainEvent.Actor,
            "payment.recorded",
            "payments",
            "Payment",
            domainEvent.PaymentId,
            $"Payment {domainEvent.ProviderPaymentId} recorded from {domainEvent.Provider} ({domainEvent.Currency} {domainEvent.Amount:N2}).",
            new
            {
                domainEvent.Provider,
                domainEvent.ProviderPaymentId,
                domainEvent.Amount,
                domainEvent.Currency,
                domainEvent.Reference
            }));

        return Task.FromResult(Result.Success());
    }
}

public sealed class PaymentLinkedAuditHandler(IAuditLogRepository repository) : IDomainEventHandler<PaymentLinkedToOrder>
{
    public Task<Result> HandleAsync(PaymentLinkedToOrder domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            domainEvent.Actor,
            "payment.linked",
            "payments",
            "Payment",
            domainEvent.PaymentId,
            "Payment linked to an order.",
            new { domainEvent.OrderId }));

        return Task.FromResult(Result.Success());
    }
}

public sealed class ReconciliationAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<ReconciliationCompleted>
{
    public Task<Result> HandleAsync(ReconciliationCompleted domainEvent, CancellationToken cancellationToken)
    {
        var action = domainEvent.Outcome switch
        {
            "Unmatched" => "reconciliation.unmatched",
            "Ambiguous" => "reconciliation.ambiguous",
            "AmountMismatch" => "reconciliation.amount_mismatch",
            "Overpaid" => "reconciliation.overpaid",
            "ManuallyMatched" => "reconciliation.manually_matched",
            _ => "reconciliation.completed"
        };

        repository.Add(AuditLog.Create(
            domainEvent.Actor,
            action,
            "reconciliation",
            "ReconciliationRecord",
            domainEvent.ReconciliationRecordId,
            $"Reconciliation finished with outcome {domainEvent.Outcome} ({domainEvent.Currency} {domainEvent.ActualAmount:N2}).",
            new
            {
                domainEvent.PaymentId,
                domainEvent.OrderId,
                domainEvent.Outcome,
                domainEvent.Strategy,
                domainEvent.Difference
            }));

        return Task.FromResult(Result.Success());
    }
}

public sealed class WebhookProcessedAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<WebhookEventProcessed>
{
    public Task<Result> HandleAsync(WebhookEventProcessed domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            AuditActor.System,
            "webhook.processed",
            "webhooks",
            "WebhookEvent",
            domainEvent.WebhookEventId,
            $"Webhook {domainEvent.ProviderEventId} from {domainEvent.Provider} processed into a payment.",
            new { domainEvent.ProviderEventId, domainEvent.PaymentId }));

        return Task.FromResult(Result.Success());
    }
}

public sealed class WebhookFailedAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<WebhookEventFailed>
{
    public Task<Result> HandleAsync(WebhookEventFailed domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            AuditActor.System,
            "webhook.failed",
            "webhooks",
            "WebhookEvent",
            domainEvent.WebhookEventId,
            $"Webhook {domainEvent.ProviderEventId} from {domainEvent.Provider} failed: {domainEvent.Reason}.",
            new { domainEvent.ProviderEventId, domainEvent.Reason }));

        return Task.FromResult(Result.Success());
    }
}

public sealed class WebhookIgnoredAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<WebhookEventIgnored>
{
    public Task<Result> HandleAsync(WebhookEventIgnored domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            AuditActor.System,
            "webhook.ignored",
            "webhooks",
            "WebhookEvent",
            domainEvent.WebhookEventId,
            $"Webhook {domainEvent.ProviderEventId} from {domainEvent.Provider} ignored: {domainEvent.Reason}.",
            new { domainEvent.ProviderEventId, domainEvent.Reason }));

        return Task.FromResult(Result.Success());
    }
}

public sealed class WebhookConflictAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<WebhookEventConflict>
{
    public Task<Result> HandleAsync(WebhookEventConflict domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            AuditActor.System,
            "webhook.conflict",
            "webhooks",
            "WebhookEvent",
            domainEvent.WebhookEventId,
            $"Webhook {domainEvent.ProviderEventId} from {domainEvent.Provider} conflicted: {domainEvent.Reason}.",
            new { domainEvent.ProviderEventId, domainEvent.Reason }));

        return Task.FromResult(Result.Success());
    }
}

public sealed class PaymentFailedAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<PaymentFailed>
{
    public Task<Result> HandleAsync(PaymentFailed domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            AuditActor.System,
            "payment.failed",
            "payments",
            "Payment",
            domainEvent.PaymentId,
            $"Payment failed: {domainEvent.Reason}.",
            new { domainEvent.Reason }));

        return Task.FromResult(Result.Success());
    }
}

public sealed class PaymentRefundedAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<PaymentRefunded>
{
    public Task<Result> HandleAsync(PaymentRefunded domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            domainEvent.Actor,
            "refund.recorded",
            "payments",
            "Refund",
            domainEvent.RefundId,
            $"Refund of {domainEvent.Currency} {domainEvent.Amount:N2} recorded for payment {domainEvent.PaymentId}. Reason: {domainEvent.Reason}.",
            new
            {
                domainEvent.PaymentId,
                domainEvent.OrderId,
                domainEvent.Amount,
                domainEvent.Currency
            }));

        return Task.FromResult(Result.Success());
    }
}

public sealed class OrderPaymentReversedAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<OrderPaymentReversed>
{
    public Task<Result> HandleAsync(OrderPaymentReversed domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            domainEvent.Actor,
            "order.payment_reversed",
            "orders",
            "Order",
            domainEvent.OrderId,
            $"Order {domainEvent.OrderNumber} released {domainEvent.Currency} {domainEvent.Amount:N2} after a refund; paid amount is now {domainEvent.PaidAmount:N2}.",
            new
            {
                domainEvent.PaymentId,
                domainEvent.Amount,
                domainEvent.PaidAmount,
                domainEvent.Status
            }));

        return Task.FromResult(Result.Success());
    }
}
