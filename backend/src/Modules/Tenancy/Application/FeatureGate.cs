using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Tenancy.Application;

public sealed class FeatureGate(IEntitlementService entitlements) : IFeatureGate
{
    public Task<Result> EnsureEnabledAsync(
        string featureKey,
        CancellationToken cancellationToken = default)
        => entitlements.EnsureFeatureAsync(featureKey, cancellationToken);
}
