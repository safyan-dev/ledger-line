using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Tenancy.Application;

public interface IEntitlementService
{
    Task<EntitlementsDto?> GetEntitlementsAsync(CancellationToken cancellationToken = default);

    Task<bool> IsFeatureEnabledAsync(string featureKey, CancellationToken cancellationToken = default);

    Task<Result> EnsureFeatureAsync(string featureKey, CancellationToken cancellationToken = default);

    Task<PlanLimitDto?> GetLimitAsync(string limitKey, CancellationToken cancellationToken = default);
}

public sealed class EntitlementService(IAppDbContext dbContext, ITenantContext tenantContext) : IEntitlementService
{
    public async Task<EntitlementsDto?> GetEntitlementsAsync(CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return null;
        }

        var subscription = await dbContext.Set<Subscription>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.TenantId == tenantId, cancellationToken);

        if (subscription is null)
        {
            return null;
        }

        var plan = await dbContext.Set<Plan>()
            .AsNoTracking()
            .Include(candidate => candidate.Features)
            .ThenInclude(planFeature => planFeature.Feature)
            .Include(candidate => candidate.Limits)
            .FirstOrDefaultAsync(candidate => candidate.Id == subscription.PlanId, cancellationToken);

        if (plan is null)
        {
            return null;
        }

        return new EntitlementsDto(
            plan.Code,
            plan.Name,
            subscription.Status.ToString(),
            PlanFormatting.OrderFeatures(plan.Features),
            PlanFormatting.OrderLimits(plan.Limits));
    }

    public async Task<bool> IsFeatureEnabledAsync(string featureKey, CancellationToken cancellationToken = default)
    {
        var entitlements = await GetEntitlementsAsync(cancellationToken);

        return entitlements is not null
               && entitlements.Features.Any(feature =>
                   string.Equals(feature.Key, featureKey, StringComparison.OrdinalIgnoreCase) && feature.Included);
    }

    public async Task<Result> EnsureFeatureAsync(string featureKey, CancellationToken cancellationToken = default)
    {
        if (await IsFeatureEnabledAsync(featureKey, cancellationToken))
        {
            return Result.Success();
        }

        return Result.Failure(Error.Forbidden(
            "plan.feature_not_included",
            $"The current plan does not include '{featureKey}'."));
    }

    public async Task<PlanLimitDto?> GetLimitAsync(string limitKey, CancellationToken cancellationToken = default)
    {
        var entitlements = await GetEntitlementsAsync(cancellationToken);

        return entitlements?.Limits.FirstOrDefault(limit =>
            string.Equals(limit.Key, limitKey, StringComparison.OrdinalIgnoreCase));
    }
}
