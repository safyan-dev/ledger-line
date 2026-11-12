using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Payments.Contracts;

public interface IPaymentsFacade
{
    Task<Result<ProviderPaymentApplyResult>> ApplyProviderPaymentAsync(
        ProviderPaymentData payment,
        bool succeeded,
        AuditActor actor,
        CancellationToken cancellationToken = default);

    Task<Result<PaymentDto>> GetPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default);

    Task<Result<PaymentDto>> GetPaymentForUpdateAsync(Guid paymentId, CancellationToken cancellationToken = default);

    Task<Result<PaymentDto>> MarkMatchedAsync(
        Guid paymentId,
        Guid orderId,
        AuditActor actor,
        CancellationToken cancellationToken = default);

    Task<Result<PaymentDto>> MarkUnmatchedAsync(
        Guid paymentId,
        string reason,
        AuditActor actor,
        CancellationToken cancellationToken = default);
}
