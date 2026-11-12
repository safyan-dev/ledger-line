using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PaymentReconciliation.Modules.Reconciliation.Application;
using PaymentReconciliation.Modules.Reconciliation.Contracts;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Web;

namespace PaymentReconciliation.Modules.Reconciliation.Endpoints;

public sealed record ManualMatchRequest(Guid PaymentId, Guid OrderId, string? Note);

public sealed record LinkPaymentRequest(Guid OrderId, string? Note);

public static class ReconciliationEndpoints
{
    public static IEndpointRouteBuilder MapReconciliationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reconciliation").WithTags("Reconciliation");

        group.MapGet("/", async (
            int? page,
            int? pageSize,
            string? outcome,
            string? search,
            Guid? paymentId,
            Guid? orderId,
            DateOnly? from,
            DateOnly? to,
            string? reason,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new GetReconciliationRecordsQuery(page ?? 1, pageSize ?? 25, outcome, search, paymentId, orderId, from, to, reason),
                cancellationToken);

            return result.ToHttpResult();
        })
        .WithName("GetReconciliationRecords")
        .Produces<PagedResult<ReconciliationRecordDto>>();

        group.MapGet("/payment/{paymentId:guid}", async (
            Guid paymentId,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new GetReconciliationForPaymentQuery(paymentId),
                cancellationToken);

            return result.ToHttpResult();
        })
        .WithName("GetReconciliationForPayment")
        .Produces<IReadOnlyList<ReconciliationRecordDto>>();

        group.MapPost("/manual-match", async (
            ManualMatchRequest request,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new ManualMatchCommand(request.PaymentId, request.OrderId, request.Note),
                cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/reconciliation/{result.Value.Id}", result.Value)
                : result.ToProblemResult();
        })
        .WithName("ManualMatch")
        .Produces<ReconciliationRecordDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        var payments = app.MapGroup("/api/payments").WithTags("Reconciliation");

        payments.MapPost("/{id:guid}/link", async (
            Guid id,
            LinkPaymentRequest request,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var note = string.IsNullOrWhiteSpace(request.Note) ? "Payment linked to an order." : request.Note;
            var result = await dispatcher.SendAsync(new ManualMatchCommand(id, request.OrderId, note), cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/reconciliation/{result.Value.Id}", result.Value)
                : result.ToProblemResult();
        })
        .WithName("LinkPaymentToOrder")
        .Produces<ReconciliationRecordDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        payments.MapPost("/{id:guid}/reconcile", async (
            Guid id,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new ReRunReconciliationCommand(id), cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/reconciliation/{result.Value.Id}", result.Value)
                : result.ToProblemResult();
        })
        .WithName("ReRunReconciliation")
        .Produces<ReconciliationRecordDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
