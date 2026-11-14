using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PaymentReconciliation.Modules.Tenancy.Application;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Web;
using PaymentReconciliation.Persistence.Abstractions;

namespace PaymentReconciliation.Modules.Tenancy.Endpoints;

public static class PlatformEndpoints
{
    public static IEndpointRouteBuilder MapPlatformEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/platform")
            .WithTags("Platform")
            .RequireAuthorization(AuthorizationPolicies.PlatformAdmin)
            .AddEndpointFilter(async (context, next) =>
            {
                var boundaryBypass = context.HttpContext.RequestServices
                    .GetRequiredService<ITenantBoundaryBypass>();
                using (boundaryBypass.Begin())
                {
                    return await next(context);
                }
            });

        group.MapGet("/dashboard", async (
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetPlatformDashboardQuery(), cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.PlatformAdmin)
        .WithName("PlatformDashboard")
        .Produces<PlatformDashboardDto>()
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/tenants", async (
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetPlatformTenantsQuery(), cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.PlatformAdmin)
        .WithName("PlatformTenants")
        .Produces<IReadOnlyList<PlatformTenantDto>>()
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/tenants/{tenantId:guid}", async (
            Guid tenantId,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new GetPlatformTenantDetailsQuery(tenantId),
                cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.PlatformAdmin)
        .WithName("PlatformTenantDetails")
        .Produces<PlatformTenantDetailsDto>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/tenants/{tenantId:guid}/status", async (
            Guid tenantId,
            PlatformTenantStatusRequest request,
            IAppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var tenant = await dbContext.Set<Tenant>()
                .FirstOrDefaultAsync(candidate => candidate.Id == tenantId, cancellationToken);
            if (tenant is null)
            {
                return Results.NotFound();
            }

            if (string.Equals(request.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                tenant.Activate();
            }
            else if (string.Equals(request.Status, "Suspended", StringComparison.OrdinalIgnoreCase))
            {
                tenant.Suspend();
            }
            else
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = ["Status must be Active or Suspended."]
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { tenant.Id, status = tenant.Status.ToString() });
        })
        .RequireAuthorization(AuthorizationPolicies.PlatformAdmin)
        .WithName("UpdatePlatformTenantStatus")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/plans", async (
            IAppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var plans = await dbContext.Set<Plan>()
                .AsNoTracking()
                .Include(plan => plan.Features)
                .ThenInclude(feature => feature.Feature)
                .Include(plan => plan.Limits)
                .OrderBy(plan => plan.SortOrder)
                .ToListAsync(cancellationToken);
            return Results.Ok(plans.Select(plan => new PlatformPlanDto(
                    plan.Code,
                    plan.Name,
                    plan.Tagline,
                    plan.Audience,
                    plan.MonthlyPrice,
                    plan.PriceNote,
                    plan.IsRecommended,
                    plan.SortOrder,
                    plan.IsActive,
                    PlanFormatting.OrderFeatures(plan.Features),
                    PlanFormatting.OrderLimits(plan.Limits))).ToList());
        })
        .RequireAuthorization(AuthorizationPolicies.PlatformAdmin)
        .WithName("PlatformPlans")
        .Produces<IReadOnlyList<PlatformPlanDto>>()
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPatch("/plans/{code}/status", async (
            string code,
            PlatformPlanStatusRequest request,
            IAppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var plan = await dbContext.Set<Plan>()
                .FirstOrDefaultAsync(candidate => candidate.Code == code.ToLower(), cancellationToken);
            if (plan is null)
            {
                return Results.NotFound();
            }

            plan.SetActive(request.IsActive);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { plan.Code, plan.IsActive });
        })
        .RequireAuthorization(AuthorizationPolicies.PlatformAdmin)
        .WithName("UpdatePlatformPlanStatus")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/plans/{code}/features/{featureKey}", async (
            string code,
            string featureKey,
            PlatformPlanFeatureRequest request,
            IAppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var plan = await dbContext.Set<Plan>()
                .Include(candidate => candidate.Features)
                .ThenInclude(feature => feature.Feature)
                .FirstOrDefaultAsync(candidate => candidate.Code == code.ToLower(), cancellationToken);
            if (plan is null)
            {
                return Results.NotFound();
            }

            var feature = plan.Features.FirstOrDefault(candidate =>
                string.Equals(candidate.Feature!.Key, featureKey, StringComparison.OrdinalIgnoreCase));
            if (feature is null)
            {
                return Results.NotFound();
            }

            feature.SetIncluded(request.Included);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { key = feature.Feature!.Key, feature.IsIncluded });
        })
        .RequireAuthorization(AuthorizationPolicies.PlatformAdmin)
        .WithName("UpdatePlatformPlanFeature")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/plans/{code}/limits/{limitKey}", async (
            string code,
            string limitKey,
            PlatformPlanLimitRequest request,
            IAppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var plan = await dbContext.Set<Plan>()
                .Include(candidate => candidate.Limits)
                .FirstOrDefaultAsync(candidate => candidate.Code == code.ToLower(), cancellationToken);
            if (plan is null)
            {
                return Results.NotFound();
            }

            var limit = plan.Limits.FirstOrDefault(candidate =>
                string.Equals(candidate.Key, limitKey, StringComparison.OrdinalIgnoreCase));
            if (limit is null)
            {
                return Results.NotFound();
            }

            if (request.Value is < 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["value"] = ["Usage limits cannot be negative."]
                });
            }

            limit.SetValue(request.Value);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { limit.Key, limit.Label, limit.Value, limit.Display });
        })
        .RequireAuthorization(AuthorizationPolicies.PlatformAdmin)
        .WithName("UpdatePlatformPlanLimit")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/subscriptions", async (
            int? page,
            int? pageSize,
            string? status,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new GetPlatformSubscriptionsQuery(page ?? 1, pageSize ?? 25, status),
                cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.PlatformAdmin)
        .WithName("PlatformSubscriptions")
        .Produces<PagedResult<PlatformSubscriptionDto>>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/subscriptions/{subscriptionId:guid}", async (
            Guid subscriptionId,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new GetPlatformSubscriptionQuery(subscriptionId),
                cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.PlatformAdmin)
        .WithName("PlatformSubscriptionDetails")
        .Produces<PlatformSubscriptionDto>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/subscriptions/{subscriptionId:guid}/activate", async (
            Guid subscriptionId,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new ActivatePlatformSubscriptionCommand(subscriptionId),
                cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.PlatformAdmin)
        .WithName("ActivatePlatformSubscription")
        .Produces<PlatformSubscriptionDto>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/subscriptions/{subscriptionId:guid}/plan", async (
            Guid subscriptionId,
            PlatformSubscriptionPlanRequest request,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new ChangePlatformSubscriptionPlanCommand(subscriptionId, request.PlanCode),
                cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.PlatformAdmin)
        .WithName("ChangePlatformSubscriptionPlan")
        .Produces<PlatformSubscriptionDto>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/subscriptions/{subscriptionId:guid}/extend", async (
            Guid subscriptionId,
            PlatformSubscriptionExtensionRequest request,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new ExtendPlatformSubscriptionCommand(subscriptionId, request.Days),
                cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.PlatformAdmin)
        .WithName("ExtendPlatformSubscription")
        .Produces<PlatformSubscriptionDto>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPatch("/subscriptions/{subscriptionId:guid}/status", async (
            Guid subscriptionId,
            PlatformSubscriptionStatusRequest request,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new ChangePlatformSubscriptionStatusCommand(subscriptionId, request.Status),
                cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.PlatformAdmin)
        .WithName("UpdatePlatformSubscriptionStatus")
        .Produces<PlatformSubscriptionDto>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}

public sealed record PlatformTenantStatusRequest(string Status);
public sealed record PlatformPlanDto(
    string Code,
    string Name,
    string Tagline,
    string Audience,
    decimal? MonthlyPrice,
    string PriceNote,
    bool Recommended,
    int SortOrder,
    bool IsActive,
    IReadOnlyList<PlanFeatureDto> Features,
    IReadOnlyList<PlanLimitDto> Limits);
public sealed record PlatformPlanStatusRequest(bool IsActive);
public sealed record PlatformPlanFeatureRequest(bool Included);
public sealed record PlatformPlanLimitRequest(int? Value);
public sealed record PlatformSubscriptionPlanRequest(string PlanCode);
public sealed record PlatformSubscriptionExtensionRequest(int Days);
public sealed record PlatformSubscriptionStatusRequest(string Status);
