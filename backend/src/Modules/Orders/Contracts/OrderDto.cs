namespace PaymentReconciliation.Modules.Orders.Contracts;

public sealed record OrderDto(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    string? CustomerReference,
    string? Description,
    decimal ExpectedAmount,
    decimal PaidAmount,
    string Currency,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public decimal RemainingAmount => decimal.Round(ExpectedAmount - PaidAmount, 2, MidpointRounding.AwayFromZero);
}

public sealed record OrderListItemDto(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    string? CustomerReference,
    decimal ExpectedAmount,
    decimal PaidAmount,
    string Currency,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record OrderCandidate(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    string? CustomerReference,
    decimal ExpectedAmount,
    decimal PaidAmount,
    string Currency,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record OrderAllocation(
    Guid OrderId,
    string OrderNumber,
    decimal ExpectedAmount,
    decimal PaidAmount,
    decimal RemainingAmount,
    string Currency,
    string Status);
