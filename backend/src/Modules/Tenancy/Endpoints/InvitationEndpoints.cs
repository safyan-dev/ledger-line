using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PaymentReconciliation.Modules.Tenancy.Application;
using PaymentReconciliation.Modules.Tenancy.Contracts;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Web;

namespace PaymentReconciliation.Modules.Tenancy.Endpoints;

public static class InvitationEndpoints
{
    public static IEndpointRouteBuilder MapInvitationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenants/current/invitations").WithTags("Invitations");

        group.MapGet("/", async (
            string? status,
            int? page,
            int? pageSize,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new GetTenantInvitationsQuery(status, page ?? 1, pageSize ?? 25),
                cancellationToken);

            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.TenantOwnerOrAdmin)
        .RequireTenantPermission(TenantPermission.InviteTeamMembers)
        .WithName("TenantInvitations")
        .Produces<PagedResult<TenantInvitationDto>>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/", async (
            CreateTenantInvitationRequest request,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new CreateTenantInvitationCommand(request.Email, request.Role),
                cancellationToken);

            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.TenantOwnerOrAdmin)
        .RequireTenantPermission(TenantPermission.InviteTeamMembers)
        .WithName("CreateTenantInvitation")
        .Produces<IssuedTenantInvitationDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/resend", async (
            Guid id,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new ResendTenantInvitationCommand(id), cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.TenantOwnerOrAdmin)
        .RequireTenantPermission(TenantPermission.InviteTeamMembers)
        .WithName("ResendTenantInvitation")
        .Produces<IssuedTenantInvitationDto>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/revoke", async (
            Guid id,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new RevokeTenantInvitationCommand(id), cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.TenantOwnerOrAdmin)
        .RequireTenantPermission(TenantPermission.InviteTeamMembers)
        .WithName("RevokeTenantInvitation")
        .Produces<TenantInvitationDto>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        var acceptGroup = app.MapGroup("/api/invitations").WithTags("Invitations");

        acceptGroup.MapPost("/accept", async (
            AcceptTenantInvitationRequest request,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new AcceptTenantInvitationCommand(request.Token, request.DisplayName, request.Password),
                cancellationToken);

            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithName("AcceptTenantInvitation")
        .Produces<AcceptTenantInvitationResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
