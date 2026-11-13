using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PaymentReconciliation.Modules.Tenancy.Application;

namespace PaymentReconciliation.Modules.Tenancy.Endpoints;

public static class PlanEndpoints
{
    public static IEndpointRouteBuilder MapPlanEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/plans", async (IPlanCatalog catalog, CancellationToken cancellationToken) =>
            {
                var plans = await catalog.GetPlansAsync(cancellationToken);
                return Results.Ok(plans);
            })
            .AllowAnonymous()
            .WithTags("Plans")
            .WithName("GetPlans")
            .Produces<IReadOnlyList<PlanDto>>();

        return app;
    }
}
