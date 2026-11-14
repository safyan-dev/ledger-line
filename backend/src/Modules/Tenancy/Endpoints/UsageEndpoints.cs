using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PaymentReconciliation.Modules.Tenancy.Application;
using PaymentReconciliation.Modules.Tenancy.Contracts;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Web;

namespace PaymentReconciliation.Modules.Tenancy.Endpoints;

public static class UsageEndpoints
{
    public static IEndpointRouteBuilder MapUsageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/usage").WithTags("Usage");

        group.MapGet("/", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetUsageQuery(), cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.TenantMember)
        .RequireTenantPermission(TenantPermission.ViewUsage)
        .WithName("CurrentUsage")
        .Produces<UsageSummaryDto>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/features/{featureKey}", async (
            string featureKey,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new GetFeatureAvailabilityQuery(featureKey),
                cancellationToken);

            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.TenantMember)
        .RequireTenantPermission(TenantPermission.ViewUsage)
        .WithName("FeatureAvailability")
        .Produces<FeatureAvailabilityDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }
}
