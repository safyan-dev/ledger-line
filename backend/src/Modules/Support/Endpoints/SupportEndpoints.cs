using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PaymentReconciliation.Modules.Support.Application;
using PaymentReconciliation.Modules.Tenancy.Contracts;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Web;
using PaymentReconciliation.Persistence.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace PaymentReconciliation.Modules.Support.Endpoints;

public static class SupportEndpoints
{
    public static IEndpointRouteBuilder MapSupportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/support/tickets").WithTags("Support");
        var platformGroup = app.MapGroup("/api/platform/support/tickets")
            .WithTags("Platform Support")
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

        platformGroup.MapGet("/", async (
            int? page,
            int? pageSize,
            string? status,
            string? category,
            string? priority,
            string? search,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new GetPlatformSupportTicketsQuery(
                    page ?? 1,
                    pageSize ?? 25,
                    status,
                    category,
                    priority,
                    search),
                cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("GetPlatformSupportTickets")
        .Produces<PagedResult<PlatformSupportTicketDto>>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        platformGroup.MapGet("/{id:guid}", async (
            Guid id,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetPlatformSupportTicketQuery(id), cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("GetPlatformSupportTicket")
        .Produces<PlatformSupportTicketDetailDto>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        platformGroup.MapPost("/{id:guid}/messages", async (
            Guid id,
            ReplySupportTicketRequest request,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new ReplyToPlatformSupportTicketCommand(id, request.Body),
                cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("ReplyToPlatformSupportTicket")
        .Produces<SupportTicketMessageDto>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        platformGroup.MapPatch("/{id:guid}/status", async (
            Guid id,
            ChangeSupportTicketStatusRequest request,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new ChangePlatformSupportTicketStatusCommand(id, request.Status),
                cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("ChangePlatformSupportTicketStatus")
        .Produces<SupportTicketDto>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/", async (
            CreateSupportTicketRequest request,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new CreateSupportTicketCommand(
                    request.Subject,
                    request.Description,
                    request.Category,
                    request.Priority),
                cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/support/tickets/{result.Value.Id}", result.Value)
                : result.ToProblemResult();
        })
        .RequireAuthorization(AuthorizationPolicies.TenantMember)
        .RequireTenantPermission(TenantPermission.CreateSupportTicket)
        .WithName("CreateSupportTicket")
        .Produces<SupportTicketDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", async (
            int? page,
            int? pageSize,
            string? status,
            string? category,
            string? priority,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new GetSupportTicketsQuery(page ?? 1, pageSize ?? 25, status, category, priority),
                cancellationToken);

            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.TenantMember)
        .RequireTenantPermission(TenantPermission.ViewSupportTicket)
        .WithName("GetSupportTickets")
        .Produces<PagedResult<SupportTicketDto>>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
            Guid id,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetSupportTicketQuery(id), cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.TenantMember)
        .RequireTenantPermission(TenantPermission.ViewSupportTicket)
        .WithName("GetSupportTicket")
        .Produces<SupportTicketDetailDto>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/messages", async (
            Guid id,
            ReplySupportTicketRequest request,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new ReplyToSupportTicketCommand(id, request.Body),
                cancellationToken);

            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.TenantMember)
        .RequireTenantPermission(TenantPermission.ReplySupportTicket)
        .WithName("ReplyToSupportTicket")
        .Produces<SupportTicketMessageDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/{id:guid}/status", async (
            Guid id,
            ChangeSupportTicketStatusRequest request,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new ChangeSupportTicketStatusCommand(id, request.Status),
                cancellationToken);

            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.TenantMember)
        .RequireTenantPermission(TenantPermission.ResolveSupportTicket)
        .WithName("ChangeSupportTicketStatus")
        .Produces<SupportTicketDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
