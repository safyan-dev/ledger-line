using PaymentReconciliation.Modules.Payments.Contracts.Events;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Domain;

namespace PaymentReconciliation.Modules.Payments.Domain;

public enum RefundStatus
{
    Succeeded,
    Failed
}

public sealed class Refund : AggregateRoot
{
    private Refund()
    {
    }

    private Refund(
        Guid id,
        Guid paymentId,
        Guid? orderId,
        decimal amount,
        string currency,
        string reason,
        string provider,
        string providerRefundId,
        RefundStatus status,
        string createdBy,
        DateTimeOffset createdAt)
        : base(id)
    {
        PaymentId = paymentId;
        OrderId = orderId;
        Amount = amount;
        Currency = currency;
        Reason = reason;
        Provider = provider;
        ProviderRefundId = providerRefundId;
        Status = status;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public Guid PaymentId { get; private set; }

    public Guid? OrderId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = "USD";

    public string Reason { get; private set; } = string.Empty;

    public string Provider { get; private set; } = "manual";

    public string ProviderRefundId { get; private set; } = string.Empty;

    public RefundStatus Status { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static Refund Create(Payment payment, decimal amount, string reason, AuditActor actor)
    {
        if (amount <= 0)
        {
            throw new DomainException("Refund amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("A refund reason is required.");
        }

        var refund = new Refund(
            Guid.NewGuid(),
            payment.Id,
            payment.OrderId,
            amount,
            payment.Currency,
            reason.Trim(),
            payment.Provider,
            $"{payment.Provider}_refund_{Guid.NewGuid():N}",
            RefundStatus.Succeeded,
            actor.Name,
            DateTimeOffset.UtcNow);

        refund.Raise(new PaymentRefunded(
            refund.Id,
            refund.PaymentId,
            refund.OrderId,
            refund.Amount,
            refund.Currency,
            refund.Reason,
            actor));

        return refund;
    }
}
