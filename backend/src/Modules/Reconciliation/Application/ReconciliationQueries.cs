using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Orders.Contracts;
using PaymentReconciliation.Modules.Payments.Contracts;
using PaymentReconciliation.Modules.Reconciliation.Contracts;
using PaymentReconciliation.Modules.Reconciliation.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Reconciliation.Application;

public sealed record GetReconciliationRecordsQuery(
    int Page = 1,
    int PageSize = 25,
    string? Outcome = null,
    string? Search = null,
    Guid? PaymentId = null,
    Guid? OrderId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Reason = null) : IQuery<PagedResult<ReconciliationRecordDto>>;

public sealed class GetReconciliationRecordsHandler(
    IAppDbContext dbContext,
    IPaymentsFacade payments,
    IOrdersFacade orders)
    : IQueryHandler<GetReconciliationRecordsQuery, PagedResult<ReconciliationRecordDto>>
{
    public async Task<Result<PagedResult<ReconciliationRecordDto>>> Handle(
        GetReconciliationRecordsQuery query,
        CancellationToken cancellationToken)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var records = dbContext.Set<ReconciliationRecord>().AsNoTracking();

        if (query.PaymentId is { } paymentId)
        {
            records = records.Where(record => record.PaymentId == paymentId);
        }

        if (query.OrderId is { } orderId)
        {
            records = records.Where(record => record.OrderId == orderId);
        }

        if (!string.IsNullOrWhiteSpace(query.Outcome)
            && Enum.TryParse<ReconciliationOutcome>(query.Outcome, ignoreCase: true, out var outcome))
        {
            records = records.Where(record => record.Outcome == outcome);
        }

        if (query.From is { } from)
        {
            var fromDate = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            records = records.Where(record => record.CreatedAt >= fromDate);
        }

        if (query.To is { } to)
        {
            var toExclusive = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            records = records.Where(record => record.CreatedAt < toExclusive);
        }

        if (!string.IsNullOrWhiteSpace(query.Reason))
        {
            var reason = query.Reason.Trim().ToLowerInvariant();
            records = records.Where(record => record.Reason != null && record.Reason.ToLower().Contains(reason));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            var lower = term.ToLowerInvariant();
            var matchingPayments = await payments.FindPaymentIdsAsync(term, cancellationToken);
            var matchingOrders = await orders.FindOrderIdsByNumberAsync(term, cancellationToken);

            records = records.Where(record =>
                matchingPayments.Contains(record.PaymentId)
                || (record.OrderId != null && matchingOrders.Contains(record.OrderId.Value))
                || (record.Reason != null && record.Reason.ToLower().Contains(lower)));
        }

        var totalCount = await records.CountAsync(cancellationToken);

        var pageRecords = await records
            .OrderByDescending(record => record.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = await ToDtosAsync(pageRecords, payments, orders, cancellationToken);

        return Result.Success(PagedResult<ReconciliationRecordDto>.Create(items, page, pageSize, totalCount));
    }

    internal static async Task<List<ReconciliationRecordDto>> ToDtosAsync(
        IReadOnlyList<ReconciliationRecord> records,
        IPaymentsFacade payments,
        IOrdersFacade orders,
        CancellationToken cancellationToken)
    {
        var paymentReferences = await payments.GetPaymentReferencesAsync(
            records.Select(record => record.PaymentId).Distinct(),
            cancellationToken);

        var orderIds = records
            .Where(record => record.OrderId.HasValue)
            .Select(record => record.OrderId!.Value)
            .Distinct()
            .ToList();
        var orderNumbers = await orders.GetOrderNumbersAsync(orderIds, cancellationToken);

        return records
            .Select(record =>
            {
                paymentReferences.TryGetValue(record.PaymentId, out var reference);
                var orderNumber = record.OrderId is { } orderId && orderNumbers.TryGetValue(orderId, out var number)
                    ? number
                    : null;

                return new ReconciliationRecordDto(
                    record.Id,
                    record.PaymentId,
                    reference?.ProviderPaymentId,
                    record.OrderId,
                    orderNumber,
                    record.Outcome.ToString(),
                    record.Strategy.ToString(),
                    record.ExpectedAmount,
                    record.ActualAmount,
                    record.Difference,
                    record.Currency,
                    record.Reason,
                    record.Notes,
                    record.MatchedBy,
                    record.CreatedAt);
            })
            .ToList();
    }
}

public sealed record GetReconciliationForPaymentQuery(Guid PaymentId) : IQuery<IReadOnlyList<ReconciliationRecordDto>>;

public sealed class GetReconciliationForPaymentHandler(
    IAppDbContext dbContext,
    IPaymentsFacade payments,
    IOrdersFacade orders)
    : IQueryHandler<GetReconciliationForPaymentQuery, IReadOnlyList<ReconciliationRecordDto>>
{
    public async Task<Result<IReadOnlyList<ReconciliationRecordDto>>> Handle(
        GetReconciliationForPaymentQuery query,
        CancellationToken cancellationToken)
    {
        var records = await dbContext.Set<ReconciliationRecord>()
            .AsNoTracking()
            .Where(record => record.PaymentId == query.PaymentId)
            .OrderByDescending(record => record.CreatedAt)
            .ToListAsync(cancellationToken);

        var items = await GetReconciliationRecordsHandler.ToDtosAsync(
            records,
            payments,
            orders,
            cancellationToken);

        return Result.Success<IReadOnlyList<ReconciliationRecordDto>>(items);
    }
}
