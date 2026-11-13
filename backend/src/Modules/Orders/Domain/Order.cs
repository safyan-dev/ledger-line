using PaymentReconciliation.Modules.Orders.Contracts.Events;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Domain;

namespace PaymentReconciliation.Modules.Orders.Domain;

public enum OrderStatus
{
    Pending,
    PartiallyPaid,
    Paid,
    Overpaid,
    Cancelled
}

public sealed class Order : AggregateRoot, ITenantOwned
{
    private readonly HashSet<Guid> _allocatedPaymentIds = [];

    private Order()
    {
    }

    private Order(
        Guid id,
        string orderNumber,
        string customerName,
        string? customerReference,
        string? description,
        Money expectedAmount,
        DateTimeOffset createdAt)
        : base(id)
    {
        OrderNumber = orderNumber;
        CustomerName = customerName;
        CustomerReference = customerReference;
        Description = description;
        ExpectedAmount = expectedAmount.Amount;
        Currency = expectedAmount.Currency;
        PaidAmount = 0m;
        Status = OrderStatus.Pending;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public string OrderNumber { get; private set; } = string.Empty;

    public Guid TenantId { get; private set; }

    public string CustomerName { get; private set; } = string.Empty;

    public string? CustomerReference { get; private set; }

    public string? Description { get; private set; }

    public decimal ExpectedAmount { get; private set; }

    public decimal PaidAmount { get; private set; }

    public string Currency { get; private set; } = "USD";

    public OrderStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Order Create(
        string orderNumber,
        string customerName,
        string? customerReference,
        string? description,
        Money expectedAmount,
        AuditActor actor,
        DateTimeOffset? createdAt = null,
        Guid? tenantId = null)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            throw new DomainException("Order number is required.");
        }

        if (string.IsNullOrWhiteSpace(customerName))
        {
            throw new DomainException("Customer name is required.");
        }

        if (expectedAmount.Amount <= 0)
        {
            throw new DomainException("Expected amount must be greater than zero.");
        }

        var order = new Order(
            Guid.NewGuid(),
            orderNumber.Trim(),
            customerName.Trim(),
            customerReference?.Trim(),
            description?.Trim(),
            expectedAmount,
            createdAt ?? DateTimeOffset.UtcNow)
        {
            TenantId = tenantId ?? Guid.Empty
        };

        order.Raise(new OrderCreated(order.Id, order.OrderNumber, order.CustomerName, order.ExpectedAmount, order.Currency, actor));
        return order;
    }

    public void AllocatePayment(Guid paymentId, Money payment, AuditActor actor)
    {
        if (_allocatedPaymentIds.Contains(paymentId))
        {
            return;
        }

        if (Status == OrderStatus.Cancelled)
        {
            throw new DomainException($"Order {OrderNumber} is cancelled and cannot receive payments.");
        }

        if (payment.Amount <= 0)
        {
            throw new DomainException("Payment amount must be greater than zero.");
        }

        if (!string.Equals(Currency, payment.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException($"Payment currency {payment.Currency} does not match order currency {Currency}.");
        }

        var previousStatus = Status;
        PaidAmount = decimal.Round(PaidAmount + payment.Amount, 2, MidpointRounding.AwayFromZero);
        _allocatedPaymentIds.Add(paymentId);
        Status = ComputeStatus();
        UpdatedAt = DateTimeOffset.UtcNow;

        Raise(new OrderPaymentAllocated(
            Id,
            OrderNumber,
            paymentId,
            payment.Amount,
            PaidAmount,
            ExpectedAmount,
            Currency,
            Status.ToString(),
            actor));

        if (previousStatus != OrderStatus.Paid && Status == OrderStatus.Paid)
        {
            Raise(new OrderFullyPaid(Id, OrderNumber, Currency, PaidAmount, actor));
        }

        if (previousStatus != OrderStatus.Overpaid && Status == OrderStatus.Overpaid)
        {
            Raise(new OrderOverpaid(Id, OrderNumber, Currency, PaidAmount, ExpectedAmount, actor));
        }
    }

    public void ReversePayment(Guid paymentId, Money amount, AuditActor actor)
    {
        if (amount.Amount <= 0)
        {
            throw new DomainException("Reversal amount must be greater than zero.");
        }

        if (!string.Equals(Currency, amount.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException($"Refund currency {amount.Currency} does not match order currency {Currency}.");
        }

        if (amount.Amount > PaidAmount)
        {
            throw new DomainException(
                $"Cannot reverse {amount.Amount:N2} {Currency}; only {PaidAmount:N2} is allocated to order {OrderNumber}.");
        }

        PaidAmount = decimal.Round(PaidAmount - amount.Amount, 2, MidpointRounding.AwayFromZero);
        Status = ComputeStatus();
        UpdatedAt = DateTimeOffset.UtcNow;

        Raise(new OrderPaymentReversed(
            Id,
            OrderNumber,
            paymentId,
            amount.Amount,
            PaidAmount,
            Currency,
            Status.ToString(),
            actor));
    }

    public void Cancel(string reason, AuditActor actor)
    {
        if (Status == OrderStatus.Cancelled)
        {
            return;
        }

        if (PaidAmount > 0)
        {
            throw new DomainException("An order with allocated payments cannot be cancelled. Reverse the payments first.");
        }

        Status = OrderStatus.Cancelled;
        UpdatedAt = DateTimeOffset.UtcNow;
        Raise(new OrderCancelled(Id, OrderNumber, reason, actor));
    }

    private OrderStatus ComputeStatus()
    {
        if (PaidAmount <= 0)
        {
            return OrderStatus.Pending;
        }

        if (PaidAmount > ExpectedAmount)
        {
            return OrderStatus.Overpaid;
        }

        return PaidAmount == ExpectedAmount ? OrderStatus.Paid : OrderStatus.PartiallyPaid;
    }
}
