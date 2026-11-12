using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PaymentReconciliation.Modules.Orders.Application;
using PaymentReconciliation.Modules.Orders.Contracts;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Web;

namespace PaymentReconciliation.Modules.Orders.Endpoints;

public sealed record CreateOrderRequest(
    string CustomerName,
    string? CustomerReference,
    string? Description,
    decimal ExpectedAmount,
    string Currency,
    string? OrderNumber);

public sealed record CancelOrderRequest(string Reason);

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders").WithTags("Orders");

        group.MapGet("/", async (
            int? page,
            int? pageSize,
            string? status,
            string? search,
            string? sort,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new GetOrdersQuery(page ?? 1, pageSize ?? 25, status, search, sort),
                cancellationToken);

            return result.ToHttpResult();
        })
        .WithName("GetOrders")
        .Produces<PagedResult<OrderListItemDto>>();

        group.MapGet("/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetOrderByIdQuery(id), cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("GetOrderById")
        .Produces<OrderDto>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
            CreateOrderRequest request,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateOrderCommand(
                request.CustomerName,
                request.CustomerReference,
                request.Description,
                request.ExpectedAmount,
                request.Currency,
                request.OrderNumber);

            var result = await dispatcher.SendAsync(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/orders/{result.Value.Id}", result.Value)
                : result.ToProblemResult();
        })
        .WithName("CreateOrder")
        .Produces<OrderDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/{id:guid}/cancel", async (
            Guid id,
            CancelOrderRequest request,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new CancelOrderCommand(id, request.Reason), cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("CancelOrder")
        .Produces<OrderDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
