using FluentValidation;
using Microsoft.Extensions.Logging;
using PaymentReconciliation.Modules.Payments.Application;
using PaymentReconciliation.Modules.Payments.Contracts;
using PaymentReconciliation.Modules.Payments.Domain;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Domain;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Payments.Application;

public sealed record RecordPaymentCommand(
    string? Provider,
    string? ProviderPaymentId,
    decimal Amount,
    string Currency,
    string? CustomerName,
    string? CustomerReference,
    string? Reference,
    DateTimeOffset? ReceivedAt) : ICommand<PaymentDto>;

public sealed class RecordPaymentValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentValidator()
    {
        RuleFor(command => command.Amount).GreaterThan(0);
        RuleFor(command => command.Currency).NotEmpty().Length(3).Matches("^[A-Za-z]{3}$");
        RuleFor(command => command.Provider).MaximumLength(32);
        RuleFor(command => command.ProviderPaymentId).MaximumLength(64);
        RuleFor(command => command.CustomerName).MaximumLength(160);
        RuleFor(command => command.CustomerReference).MaximumLength(64);
        RuleFor(command => command.Reference).MaximumLength(64);
    }
}

public sealed class RecordPaymentHandler(
    IPaymentRepository repository,
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    ILogger<RecordPaymentHandler> logger)
    : ICommandHandler<RecordPaymentCommand, PaymentDto>
{
    public async Task<Result<PaymentDto>> Handle(RecordPaymentCommand command, CancellationToken cancellationToken)
    {
        var provider = string.IsNullOrWhiteSpace(command.Provider) ? "manual" : command.Provider.Trim().ToLowerInvariant();
        var providerPaymentId = string.IsNullOrWhiteSpace(command.ProviderPaymentId)
            ? $"manual_{Guid.NewGuid():N}"
            : command.ProviderPaymentId.Trim();

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = PaymentReconciliation.SharedKernel.Diagnostics.CorrelationContext.Current,
            ["Provider"] = provider,
            ["ProviderPaymentId"] = providerPaymentId
        });

        logger.LogInformation(
            "Recording payment {ProviderPaymentId} from {Provider}.",
            providerPaymentId,
            provider);

        var existing = await repository.GetByProviderPaymentIdAsync(provider, providerPaymentId, cancellationToken);
        if (existing is not null)
        {
            logger.LogInformation(
                "Payment {ProviderPaymentId} already exists as {PaymentId}; returning it.",
                providerPaymentId,
                existing.Id);
            return Result.Success(existing.ToDto());
        }

        var payment = Payment.Create(
            provider,
            providerPaymentId,
            new Money(command.Amount, command.Currency),
            command.CustomerName,
            command.CustomerReference,
            command.Reference,
            command.ReceivedAt ?? DateTimeOffset.UtcNow,
            currentUser.Actor,
            tenantId: tenantContext.TenantId);

        repository.Add(payment);

        logger.LogInformation(
            "Payment {ProviderPaymentId} recorded as {PaymentId}.",
            providerPaymentId,
            payment.Id);

        return Result.Success(payment.ToDto());
    }
}

internal static class PaymentMapping
{
    public static PaymentDto ToDto(this Payment payment) => new(
        payment.Id,
        payment.Provider,
        payment.ProviderPaymentId,
        payment.OrderId,
        payment.ProviderOrderId,
        null,
        payment.Amount,
        payment.RefundedAmount,
        payment.Currency,
        payment.Status.ToString(),
        payment.CustomerName,
        payment.CustomerReference,
        payment.Reference,
        payment.FailureReason,
        payment.ReceivedAt,
        payment.CreatedAt,
        payment.UpdatedAt);

    public static PaymentDto ToDto(this Payment payment, string? orderNumber) => new(
        payment.Id,
        payment.Provider,
        payment.ProviderPaymentId,
        payment.OrderId,
        payment.ProviderOrderId,
        orderNumber,
        payment.Amount,
        payment.RefundedAmount,
        payment.Currency,
        payment.Status.ToString(),
        payment.CustomerName,
        payment.CustomerReference,
        payment.Reference,
        payment.FailureReason,
        payment.ReceivedAt,
        payment.CreatedAt,
        payment.UpdatedAt);

    public static RefundDto ToDto(this Refund refund) => new(
        refund.Id,
        refund.PaymentId,
        refund.OrderId,
        refund.Amount,
        refund.Currency,
        refund.Reason,
        refund.Status.ToString(),
        refund.Provider,
        refund.ProviderRefundId,
        refund.CreatedBy,
        refund.CreatedAt);
}
