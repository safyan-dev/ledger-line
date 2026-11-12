using PaymentReconciliation.Modules.Payments.Domain;

namespace PaymentReconciliation.Modules.Payments.Application;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Payment?> GetByProviderPaymentIdAsync(
        string provider,
        string providerPaymentId,
        CancellationToken cancellationToken = default);

    Task LockForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(Payment payment);
}
