using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Orders.Application;
using PaymentReconciliation.Modules.Orders.Contracts;
using PaymentReconciliation.Modules.Orders.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Orders.Application;

public sealed record GetOrdersQuery(
    int Page = 1,
    int PageSize = 25,
    string? Status = null,
    string? Search = null,
    string? Sort = null) : IQuery<PagedResult<OrderListItemDto>>;

public sealed class GetOrdersHandler(IAppDbContext dbContext)
    : IQueryHandler<GetOrdersQuery, PagedResult<OrderListItemDto>>
{
    public async Task<Result<PagedResult<OrderListItemDto>>> Handle(
        GetOrdersQuery query,
        CancellationToken cancellationToken)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var orders = dbContext.Set<Order>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<OrderStatus>(query.Status, ignoreCase: true, out var status))
        {
            orders = orders.Where(order => order.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            orders = orders.Where(order =>
                order.OrderNumber.ToLower().Contains(search)
                || order.CustomerName.ToLower().Contains(search)
                || (order.CustomerReference != null && order.CustomerReference.ToLower().Contains(search)));
        }

        var totalCount = await orders.CountAsync(cancellationToken);

        var ordered = query.Sort?.Trim().ToLowerInvariant() switch
        {
            "created_asc" => orders.OrderBy(order => order.CreatedAt),
            "amount_desc" => orders.OrderByDescending(order => order.ExpectedAmount),
            "amount_asc" => orders.OrderBy(order => order.ExpectedAmount),
            "order_asc" => orders.OrderBy(order => order.OrderNumber),
            "order_desc" => orders.OrderByDescending(order => order.OrderNumber),
            _ => orders.OrderByDescending(order => order.CreatedAt)
        };

        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(order => new OrderListItemDto(
                order.Id,
                order.OrderNumber,
                order.CustomerName,
                order.CustomerReference,
                order.ExpectedAmount,
                order.PaidAmount,
                order.Currency,
                order.Status.ToString(),
                order.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(PagedResult<OrderListItemDto>.Create(items, page, pageSize, totalCount));
    }
}

public sealed record GetOrderByIdQuery(Guid OrderId) : IQuery<OrderDto>;

public sealed class GetOrderByIdHandler(IAppDbContext dbContext)
    : IQueryHandler<GetOrderByIdQuery, OrderDto>
{
    public async Task<Result<OrderDto>> Handle(GetOrderByIdQuery query, CancellationToken cancellationToken)
    {
        var order = await dbContext.Set<Order>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == query.OrderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<OrderDto>(
                Error.NotFound("order.not_found", $"Order {query.OrderId} was not found."));
        }

        return Result.Success(order.ToDto());
    }
}
