namespace PaymentReconciliation.Modules.Payments.Contracts;

public sealed record PaymentDto(
    Guid Id,
    string Provider,
    string ProviderPaymentId,
    Guid? OrderId,
    Guid? ProviderOrderId,
    string? OrderNumber,
    decimal Amount,
    decimal RefundedAmount,
    string Currency,
    string Status,
    string? CustomerName,
    string? CustomerReference,
    string? Reference,
    string? FailureReason,
    DateTimeOffset ReceivedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public decimal RefundableAmount => decimal.Round(Amount - RefundedAmount, 2, MidpointRounding.AwayFromZero);
}

public sealed record PaymentListItemDto(
    Guid Id,
    string Provider,
    string ProviderPaymentId,
    Guid? OrderId,
    string? OrderNumber,
    decimal Amount,
    decimal RefundedAmount,
    string Currency,
    string Status,
    string? CustomerName,
    string? CustomerReference,
    string? Reference,
    DateTimeOffset ReceivedAt);

public sealed record RefundDto(
    Guid Id,
    Guid PaymentId,
    Guid? OrderId,
    decimal Amount,
    string Currency,
    string Reason,
    string Status,
    string Provider,
    string ProviderRefundId,
    string CreatedBy,
    DateTimeOffset CreatedAt);

public sealed record ProviderPaymentData(
    string Provider,
    string ProviderPaymentId,
    Guid? OrderId,
    decimal Amount,
    string Currency,
    string? CustomerName,
    string? CustomerReference,
    string? Reference,
    DateTimeOffset ReceivedAt,
    string? FailureReason);

public sealed record ProviderPaymentApplyResult(
    PaymentDto Payment,
    bool IsNew,
    bool Conflict,
    string? ConflictReason);
