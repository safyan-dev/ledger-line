using PaymentReconciliation.Modules.Tenancy.Contracts;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Tenancy.Application;

public sealed record GetUsageQuery : IQuery<UsageSummaryDto>;

public sealed class GetUsageHandler(IUsageService usage, ITenantAuthorizer authorizer)
    : IQueryHandler<GetUsageQuery, UsageSummaryDto>
{
    public async Task<Result<UsageSummaryDto>> Handle(
        GetUsageQuery query,
        CancellationToken cancellationToken)
    {
        var authorization = authorizer.Require(TenantPermission.ViewUsage);
        if (authorization.IsFailure)
        {
            return Result.Failure<UsageSummaryDto>(authorization.Error!);
        }

        var summary = await usage.GetUsageAsync(cancellationToken);

        return summary is null
            ? Result.Failure<UsageSummaryDto>(Error.NotFound(
                "subscription.not_found",
                "This tenant does not have a subscription."))
            : Result.Success(summary);
    }
}

public sealed record GetFeatureAvailabilityQuery(string FeatureKey)
    : IQuery<FeatureAvailabilityDto>;

public sealed class GetFeatureAvailabilityHandler(IUsageService usage, ITenantAuthorizer authorizer)
    : IQueryHandler<GetFeatureAvailabilityQuery, FeatureAvailabilityDto>
{
    public async Task<Result<FeatureAvailabilityDto>> Handle(
        GetFeatureAvailabilityQuery query,
        CancellationToken cancellationToken)
    {
        var authorization = authorizer.Require(TenantPermission.ViewUsage);
        if (authorization.IsFailure)
        {
            return Result.Failure<FeatureAvailabilityDto>(authorization.Error!);
        }

        if (string.IsNullOrWhiteSpace(query.FeatureKey))
        {
            return Result.Failure<FeatureAvailabilityDto>(Error.Validation(
                "usage.invalid_feature",
                "A feature key is required."));
        }

        var enabled = await usage.IsFeatureEnabledAsync(query.FeatureKey.Trim(), cancellationToken);
        return Result.Success(new FeatureAvailabilityDto(query.FeatureKey.Trim(), enabled));
    }
}
