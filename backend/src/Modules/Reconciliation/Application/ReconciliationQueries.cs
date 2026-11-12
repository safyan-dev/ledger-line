using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Orders.Domain;
using PaymentReconciliation.Modules.Payments.Domain;
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

public sealed class GetReconciliationRecordsHandler(IAppDbContext dbContext)
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

        var payments = dbContext.Set<Payment>().AsNoTracking();
        var orders = dbContext.Set<Order>().AsNoTracking();

        var joined = from record in records
                     join payment in payments on record.PaymentId equals payment.Id
                     join order in orders on record.OrderId equals (Guid?)order.Id into matchedOrders
                     from order in matchedOrders.DefaultIfEmpty()
                     select new { Record = record, Payment = payment, Order = order };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            joined = joined.Where(item =>
                item.Payment.ProviderPaymentId.ToLower().Contains(search)
                || (item.Order != null && item.Order.OrderNumber.ToLower().Contains(search))
                || (item.Payment.CustomerName != null && item.Payment.CustomerName.ToLower().Contains(search))
                || (item.Record.Reason != null && item.Record.Reason.ToLower().Contains(search)));
        }

        var totalCount = await joined.CountAsync(cancellationToken);

        var items = await joined
            .OrderByDescending(item => item.Record.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new ReconciliationRecordDto(
                item.Record.Id,
                item.Record.PaymentId,
                item.Payment.ProviderPaymentId,
                item.Record.OrderId,
                item.Order == null ? null : item.Order.OrderNumber,
                item.Record.Outcome.ToString(),
                item.Record.Strategy.ToString(),
                item.Record.ExpectedAmount,
                item.Record.ActualAmount,
                item.Record.Difference,
                item.Record.Currency,
                item.Record.Reason,
                item.Record.Notes,
                item.Record.MatchedBy,
                item.Record.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(PagedResult<ReconciliationRecordDto>.Create(items, page, pageSize, totalCount));
    }
}

public sealed record GetReconciliationForPaymentQuery(Guid PaymentId) : IQuery<IReadOnlyList<ReconciliationRecordDto>>;

public sealed class GetReconciliationForPaymentHandler(IAppDbContext dbContext)
    : IQueryHandler<GetReconciliationForPaymentQuery, IReadOnlyList<ReconciliationRecordDto>>
{
    public async Task<Result<IReadOnlyList<ReconciliationRecordDto>>> Handle(
        GetReconciliationForPaymentQuery query,
        CancellationToken cancellationToken)
    {
        var records = dbContext.Set<ReconciliationRecord>().AsNoTracking()
            .Where(record => record.PaymentId == query.PaymentId);

        var payments = dbContext.Set<Payment>().AsNoTracking();
        var orders = dbContext.Set<Order>().AsNoTracking();

        var items = await (from record in records
                           join payment in payments on record.PaymentId equals payment.Id
                           join order in orders on record.OrderId equals (Guid?)order.Id into matchedOrders
                           from order in matchedOrders.DefaultIfEmpty()
                           orderby record.CreatedAt descending
                           select new ReconciliationRecordDto(
                               record.Id,
                               record.PaymentId,
                               payment.ProviderPaymentId,
                               record.OrderId,
                               order == null ? null : order.OrderNumber,
                               record.Outcome.ToString(),
                               record.Strategy.ToString(),
                               record.ExpectedAmount,
                               record.ActualAmount,
                               record.Difference,
                               record.Currency,
                               record.Reason,
                               record.Notes,
                               record.MatchedBy,
                               record.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ReconciliationRecordDto>>(items);
    }
}
