using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Orders.Contracts;
using PaymentReconciliation.Modules.Payments.Contracts;
using PaymentReconciliation.Modules.Payments.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Payments.Application;

public sealed record GetPaymentsQuery(
    int Page = 1,
    int PageSize = 25,
    string? Status = null,
    string? Provider = null,
    string? Search = null,
    Guid? OrderId = null) : IQuery<PagedResult<PaymentListItemDto>>;

public sealed class GetPaymentsHandler(IAppDbContext dbContext, IOrdersFacade ordersFacade)
    : IQueryHandler<GetPaymentsQuery, PagedResult<PaymentListItemDto>>
{
    public async Task<Result<PagedResult<PaymentListItemDto>>> Handle(
        GetPaymentsQuery query,
        CancellationToken cancellationToken)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var payments = dbContext.Set<Payment>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<PaymentStatus>(query.Status, ignoreCase: true, out var status))
        {
            payments = payments.Where(payment => payment.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Provider))
        {
            var provider = query.Provider.Trim().ToLowerInvariant();
            payments = payments.Where(payment => payment.Provider == provider);
        }

        if (query.OrderId is { } orderId)
        {
            payments = payments.Where(payment => payment.OrderId == orderId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            payments = payments.Where(payment =>
                payment.ProviderPaymentId.ToLower().Contains(search)
                || (payment.CustomerName != null && payment.CustomerName.ToLower().Contains(search))
                || (payment.CustomerReference != null && payment.CustomerReference.ToLower().Contains(search))
                || (payment.Reference != null && payment.Reference.ToLower().Contains(search)));
        }

        var totalCount = await payments.CountAsync(cancellationToken);

        var items = await payments
            .OrderByDescending(payment => payment.ReceivedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(payment => new PaymentListItemDto(
                payment.Id,
                payment.Provider,
                payment.ProviderPaymentId,
                payment.OrderId,
                null,
                payment.Amount,
                payment.RefundedAmount,
                payment.Currency,
                payment.Status.ToString(),
                payment.CustomerName,
                payment.CustomerReference,
                payment.Reference,
                payment.ReceivedAt))
            .ToListAsync(cancellationToken);

        var orderIds = items
            .Where(item => item.OrderId.HasValue)
            .Select(item => item.OrderId!.Value)
            .Distinct()
            .ToList();

        var orderNumbers = await ordersFacade.GetOrderNumbersAsync(orderIds, cancellationToken);

        items = items
            .Select(item => item.OrderId is { } id && orderNumbers.TryGetValue(id, out var orderNumber)
                ? item with { OrderNumber = orderNumber }
                : item)
            .ToList();

        return Result.Success(PagedResult<PaymentListItemDto>.Create(items, page, pageSize, totalCount));
    }
}

public sealed record GetPaymentByIdQuery(Guid PaymentId) : IQuery<PaymentDto>;

public sealed class GetPaymentByIdHandler(IAppDbContext dbContext, IOrdersFacade ordersFacade)
    : IQueryHandler<GetPaymentByIdQuery, PaymentDto>
{
    public async Task<Result<PaymentDto>> Handle(
        GetPaymentByIdQuery query,
        CancellationToken cancellationToken)
    {
        var payment = await dbContext.Set<Payment>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == query.PaymentId, cancellationToken);

        if (payment is null)
        {
            return Result.Failure<PaymentDto>(
                Error.NotFound("payment.not_found", $"Payment {query.PaymentId} was not found."));
        }

        string? orderNumber = null;
        if (payment.OrderId is { } orderId)
        {
            var orderNumbers = await ordersFacade.GetOrderNumbersAsync([orderId], cancellationToken);
            orderNumber = orderNumbers.GetValueOrDefault(orderId);
        }

        return Result.Success(payment.ToDto(orderNumber));
    }
}
