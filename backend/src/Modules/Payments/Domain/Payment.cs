using PaymentReconciliation.Modules.Payments.Contracts.Events;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Domain;

namespace PaymentReconciliation.Modules.Payments.Domain;

public enum PaymentStatus
{
    Received,
    Matched,
    Unmatched,
    Failed,
    PartiallyRefunded,
    Refunded
}

public sealed class Payment : AggregateRoot, ITenantOwned
{
    private Payment()
    {
    }

    private Payment(
        Guid id,
        string provider,
        string providerPaymentId,
        Guid? providerOrderId,
        Money amount,
        string? customerName,
        string? customerReference,
        string? reference,
        DateTimeOffset receivedAt,
        DateTimeOffset createdAt)
        : base(id)
    {
        Provider = provider;
        ProviderPaymentId = providerPaymentId;
        ProviderOrderId = providerOrderId;
        Amount = amount.Amount;
        Currency = amount.Currency;
        CustomerName = customerName;
        CustomerReference = customerReference;
        Reference = reference;
        ReceivedAt = receivedAt;
        Status = PaymentStatus.Received;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public string Provider { get; private set; } = string.Empty;

    public Guid TenantId { get; private set; }

    public string ProviderPaymentId { get; private set; } = string.Empty;

    public Guid? OrderId { get; private set; }

    public Guid? ProviderOrderId { get; private set; }

    public decimal Amount { get; private set; }

    public decimal RefundedAmount { get; private set; }

    public decimal RefundableAmount => decimal.Round(Amount - RefundedAmount, 2, MidpointRounding.AwayFromZero);

    public string Currency { get; private set; } = "USD";

    public PaymentStatus Status { get; private set; }

    public string? CustomerName { get; private set; }

    public string? CustomerReference { get; private set; }

    public string? Reference { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Payment Create(
        string provider,
        string providerPaymentId,
        Money amount,
        string? customerName,
        string? customerReference,
        string? reference,
        DateTimeOffset receivedAt,
        AuditActor actor,
        DateTimeOffset? createdAt = null,
        Guid? providerOrderId = null,
        Guid? tenantId = null)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new DomainException("Payment provider is required.");
        }

        if (string.IsNullOrWhiteSpace(providerPaymentId))
        {
            throw new DomainException("Provider payment id is required.");
        }

        if (amount.Amount <= 0)
        {
            throw new DomainException("Payment amount must be greater than zero.");
        }

        var payment = new Payment(
            Guid.NewGuid(),
            provider.Trim().ToLowerInvariant(),
            providerPaymentId.Trim(),
            providerOrderId,
            amount,
            customerName?.Trim(),
            customerReference?.Trim(),
            reference?.Trim(),
            receivedAt,
            createdAt ?? DateTimeOffset.UtcNow)
        {
            TenantId = tenantId ?? Guid.Empty
        };

        payment.Raise(new PaymentRecorded(
            payment.Id,
            payment.Provider,
            payment.ProviderPaymentId,
            providerOrderId,
            payment.Amount,
            payment.Currency,
            payment.Reference,
            payment.CustomerReference,
            payment.ReceivedAt,
            actor));

        return payment;
    }

    public static Payment CreateFailed(
        string provider,
        string providerPaymentId,
        Money amount,
        string? customerName,
        string? customerReference,
        string? reference,
        DateTimeOffset receivedAt,
        string failureReason,
        AuditActor actor,
        Guid? providerOrderId = null,
        DateTimeOffset? createdAt = null,
        Guid? tenantId = null)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
        {
            failureReason = "Provider reported a failed payment.";
        }

        var payment = new Payment(
            Guid.NewGuid(),
            provider.Trim().ToLowerInvariant(),
            providerPaymentId.Trim(),
            providerOrderId,
            amount,
            customerName,
            customerReference,
            reference,
            receivedAt,
            createdAt ?? DateTimeOffset.UtcNow)
        {
            Status = PaymentStatus.Failed,
            FailureReason = failureReason.Trim(),
            TenantId = tenantId ?? Guid.Empty
        };

        payment.Raise(new PaymentFailed(payment.Id, payment.FailureReason, actor));

        return payment;
    }

    public void MarkProviderSucceeded(Guid? orderId, AuditActor actor)
    {
        if (Status is PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded)
        {
            throw new DomainException("A refunded payment cannot be marked succeeded.");
        }

        if (orderId is { } resolvedOrderId)
        {
            if (Status == PaymentStatus.Matched && OrderId == resolvedOrderId)
            {
                return;
            }

            if (ProviderOrderId is not null && ProviderOrderId != resolvedOrderId)
            {
                throw new DomainException("Provider order id does not match the stored payment.");
            }

            if (OrderId is not null && OrderId != resolvedOrderId)
            {
                throw new DomainException("Payment is already linked to another order.");
            }

            ProviderOrderId = resolvedOrderId;
        }

        Status = PaymentStatus.Received;
        FailureReason = null;
        UpdatedAt = DateTimeOffset.UtcNow;

        Raise(new PaymentRecorded(
            Id,
            Provider,
            ProviderPaymentId,
            orderId,
            Amount,
            Currency,
            Reference,
            CustomerReference,
            ReceivedAt,
            actor));
    }

    public void LinkToOrder(Guid orderId, AuditActor actor)
    {
        if (OrderId == orderId)
        {
            return;
        }

        if (OrderId is not null)
        {
            throw new DomainException("Payment is already linked to another order.");
        }

        OrderId = orderId;
        UpdatedAt = DateTimeOffset.UtcNow;
        Raise(new PaymentLinkedToOrder(Id, orderId, actor));
    }

    public void MarkMatched(Guid orderId, AuditActor actor)
    {
        if (Status is PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded)
        {
            throw new DomainException("A refunded payment cannot be matched.");
        }

        if (Status == PaymentStatus.Matched && OrderId == orderId)
        {
            return;
        }

        OrderId = orderId;
        Status = PaymentStatus.Matched;
        FailureReason = null;
        UpdatedAt = DateTimeOffset.UtcNow;
        Raise(new PaymentMatched(Id, orderId, actor));
    }

    public void MarkUnmatched(string reason, AuditActor actor)
    {
        if (Status == PaymentStatus.Matched)
        {
            throw new DomainException("A matched payment cannot be marked unmatched.");
        }

        if (Status is PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded)
        {
            throw new DomainException("A refunded payment cannot be marked unmatched.");
        }

        Status = PaymentStatus.Unmatched;
        FailureReason = reason;
        UpdatedAt = DateTimeOffset.UtcNow;
        Raise(new PaymentMarkedUnmatched(Id, reason, actor));
    }

    public void MarkFailed(string reason, AuditActor actor)
    {
        if (Status is PaymentStatus.Matched or PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded)
        {
            throw new DomainException("A matched or refunded payment cannot be marked failed.");
        }

        Status = PaymentStatus.Failed;
        FailureReason = reason;
        UpdatedAt = DateTimeOffset.UtcNow;
        Raise(new PaymentFailed(Id, reason, actor));
    }

    public void RecordRefund(decimal amount)
    {
        if (amount <= 0)
        {
            throw new DomainException("Refund amount must be greater than zero.");
        }

        if (Status == PaymentStatus.Failed)
        {
            throw new DomainException("A failed payment cannot be refunded.");
        }

        if (amount > RefundableAmount)
        {
            throw new DomainException(
                $"Refund of {amount:N2} exceeds the refundable amount of {RefundableAmount:N2} {Currency}.");
        }

        RefundedAmount = decimal.Round(RefundedAmount + amount, 2, MidpointRounding.AwayFromZero);
        Status = RefundedAmount == Amount ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;
        FailureReason = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
