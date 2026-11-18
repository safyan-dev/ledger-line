using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.SharedKernel.Abstractions;

public interface IFeatureGate
{
    Task<Result> EnsureEnabledAsync(
        string featureKey,
        CancellationToken cancellationToken = default);
}
