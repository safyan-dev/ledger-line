using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PaymentReconciliation.Modules.Payments.Application;
using PaymentReconciliation.Modules.Payments.Contracts;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Web;

namespace PaymentReconciliation.Modules.Payments.Endpoints;

public sealed record RecordPaymentRequest(
    string? Provider,
    string? ProviderPaymentId,
    decimal Amount,
    string Currency,
    string? CustomerName,
    string? CustomerReference,
    string? Reference,
    DateTimeOffset? ReceivedAt);

public sealed record RefundPaymentRequest(decimal Amount, string Reason);

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments").WithTags("Payments");

        group.MapGet("/", async (
            int? page,
            int? pageSize,
            string? status,
            string? provider,
            string? search,
            Guid? orderId,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new GetPaymentsQuery(page ?? 1, pageSize ?? 25, status, provider, search, orderId),
                cancellationToken);

            return result.ToHttpResult();
        })
        .WithName("GetPayments")
        .Produces<PagedResult<PaymentListItemDto>>();

        group.MapGet("/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetPaymentByIdQuery(id), cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("GetPaymentById")
        .Produces<PaymentDto>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
            RecordPaymentRequest request,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var command = new RecordPaymentCommand(
                request.Provider,
                request.ProviderPaymentId,
                request.Amount,
                request.Currency,
                request.CustomerName,
                request.CustomerReference,
                request.Reference,
                request.ReceivedAt);

            var result = await dispatcher.SendAsync(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/payments/{result.Value.Id}", result.Value)
                : result.ToProblemResult();
        })
        .WithName("RecordPayment")
        .Produces<PaymentDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/{id:guid}/refunds", async (
            Guid id,
            RefundPaymentRequest request,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new RefundPaymentCommand(id, request.Amount, request.Reason),
                cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/payments/{id}/refunds/{result.Value.Id}", result.Value)
                : result.ToProblemResult();
        })
        .WithName("RefundPayment")
        .Produces<RefundDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}/refunds", async (
            Guid id,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetRefundsForPaymentQuery(id), cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("GetRefundsForPayment")
        .Produces<IReadOnlyList<RefundDto>>();

        return app;
    }
}
