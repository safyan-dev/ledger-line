using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PaymentReconciliation.Modules.Tenancy.Application;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Web;

namespace PaymentReconciliation.Modules.Tenancy.Endpoints;

public static class TenantEndpoints
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenants").WithTags("Tenancy");

        group.MapPost("/register", async (
            RegisterTenantRequest request,
            TenantRegistrationService registrationService,
            CancellationToken cancellationToken) =>
        {
            var result = await registrationService.RegisterAsync(request, cancellationToken);
            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithName("RegisterTenant")
        .Produces<RegisterTenantResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/mine", async (
            TenantQueries queries,
            CancellationToken cancellationToken) =>
        {
            var result = await queries.GetMembershipsAsync(cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("MyTenants")
        .Produces<IReadOnlyList<MembershipDto>>()
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/current", async (
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetCurrentTenantQuery(), cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.TenantMember)
        .WithName("CurrentTenant")
        .Produces<TenantSettingsDto>()
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPut("/current", async (
            RenameTenantRequest request,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new RenameTenantCommand(request.Name), cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.TenantOwnerOrAdmin)
        .WithName("RenameTenant")
        .Produces<TenantSettingsDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/current/members", async (
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetTenantMembersQuery(), cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.TenantMember)
        .WithName("TenantMembers")
        .Produces<IReadOnlyList<TenantMemberDto>>()
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/current/members", async (
            AddTenantMemberRequest request,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new AddTenantMemberCommand(request.Email, request.Role),
                cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.TenantOwnerOrAdmin)
        .WithName("AddTenantMember")
        .Produces<TenantMemberDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/current/members/{userId:guid}", async (
            Guid userId,
            ChangeTenantMemberRoleRequest request,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new ChangeTenantMemberRoleCommand(userId, request.Role),
                cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.TenantOwnerOrAdmin)
        .WithName("ChangeTenantMemberRole")
        .Produces<TenantMemberDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/current/subscription", async (
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetSubscriptionQuery(), cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.TenantMember)
        .WithName("CurrentSubscription")
        .Produces<SubscriptionDto>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/current/subscription", async (
            ChangeSubscriptionPlanRequest request,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new ChangeSubscriptionPlanCommand(request.PlanCode),
                cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.TenantOwnerOrAdmin)
        .WithName("ChangeSubscriptionPlan")
        .Produces<SubscriptionDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/current/subscription/cancel", async (
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new CancelSubscriptionCommand(), cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.TenantOwnerOrAdmin)
        .WithName("CancelSubscription")
        .Produces<SubscriptionDto>()
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/current/subscription/renew", async (
            RenewSubscriptionRequest request,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new RenewSubscriptionCommand(request.IdempotencyKey),
                cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.TenantOwnerOrAdmin)
        .WithName("RenewSubscription")
        .Produces<SubscriptionDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/current/entitlements", async (
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetEntitlementsQuery(), cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.TenantMember)
        .WithName("CurrentEntitlements")
        .Produces<EntitlementsDto>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/current/members/{userId:guid}", async (
            Guid userId,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new RemoveTenantMemberCommand(userId), cancellationToken);
            return result.IsFailure ? result.Error!.ToProblemResult() : Results.NoContent();
        })
        .RequireAuthorization(AuthorizationPolicies.TenantOwnerOrAdmin)
        .WithName("RemoveTenantMember")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
