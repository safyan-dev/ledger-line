using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Orders.Application;
using PaymentReconciliation.Modules.Orders.Contracts;
using PaymentReconciliation.Modules.Orders.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Domain;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Orders.Infrastructure;

public sealed class OrdersFacade(IOrderRepository repository, IAppDbContext dbContext) : IOrdersFacade
{
    public async Task<Result<OrderAllocation>> AllocatePaymentAsync(
        Guid orderId,
        decimal amount,
        string currency,
        Guid paymentId,
        AuditActor actor,
        CancellationToken cancellationToken = default)
    {
        await repository.LockForUpdateAsync(orderId, cancellationToken);

        var order = await repository.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure<OrderAllocation>(
                Error.NotFound("order.not_found", $"Order {orderId} was not found."));
        }

        order.AllocatePayment(paymentId, new Money(amount, currency), actor);

        return Result.Success(new OrderAllocation(
            order.Id,
            order.OrderNumber,
            order.ExpectedAmount,
            order.PaidAmount,
            decimal.Round(order.ExpectedAmount - order.PaidAmount, 2, MidpointRounding.AwayFromZero),
            order.Currency,
            order.Status.ToString()));
    }

    public async Task<Result<OrderAllocation>> ReversePaymentAllocationAsync(
        Guid orderId,
        Guid paymentId,
        decimal amount,
        string currency,
        AuditActor actor,
        CancellationToken cancellationToken = default)
    {
        await repository.LockForUpdateAsync(orderId, cancellationToken);

        var order = await repository.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure<OrderAllocation>(
                Error.NotFound("order.not_found", $"Order {orderId} was not found."));
        }

        order.ReversePayment(paymentId, new Money(amount, currency), actor);

        return Result.Success(new OrderAllocation(
            order.Id,
            order.OrderNumber,
            order.ExpectedAmount,
            order.PaidAmount,
            decimal.Round(order.ExpectedAmount - order.PaidAmount, 2, MidpointRounding.AwayFromZero),
            order.Currency,
            order.Status.ToString()));
    }

    public async Task<Result<IReadOnlyList<OrderCandidate>>> FindCandidatesByOrderNumberAsync(
        string orderNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return Result.Success<IReadOnlyList<OrderCandidate>>([]);
        }

        var normalized = orderNumber.Trim().ToLowerInvariant();

        var candidates = await ToCandidatesAsync(
            CandidateOrders().Where(order => order.OrderNumber.ToLower() == normalized),
            cancellationToken);

        return Result.Success<IReadOnlyList<OrderCandidate>>(candidates);
    }

    public async Task<Result<IReadOnlyList<OrderCandidate>>> FindCandidatesByCustomerAndAmountAsync(
        string customerReference,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerReference))
        {
            return Result.Success<IReadOnlyList<OrderCandidate>>([]);
        }

        var normalizedReference = customerReference.Trim().ToLowerInvariant();
        var normalizedCurrency = currency.Trim().ToUpperInvariant();

        var candidates = await ToCandidatesAsync(
            CandidateOrders()
                .Where(order => order.CustomerReference != null && order.CustomerReference.ToLower() == normalizedReference)
                .Where(order => order.ExpectedAmount == amount && order.Currency == normalizedCurrency),
            cancellationToken);

        return Result.Success<IReadOnlyList<OrderCandidate>>(candidates);
    }

    public async Task<Result<IReadOnlyList<OrderCandidate>>> FindCandidatesByAmountAndDateAsync(
        decimal amount,
        string currency,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken = default)
    {
        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        var from = receivedAt.AddDays(-3);
        var to = receivedAt.AddDays(3);

        var candidates = await ToCandidatesAsync(
            CandidateOrders()
                .Where(order => order.ExpectedAmount == amount && order.Currency == normalizedCurrency)
                .Where(order => order.CreatedAt >= from && order.CreatedAt <= to),
            cancellationToken);

        return Result.Success<IReadOnlyList<OrderCandidate>>(candidates);
    }

    public async Task<Result<OrderCandidate>> GetCandidateAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var candidates = await ToCandidatesAsync(
            CandidateOrders().Where(order => order.Id == orderId),
            cancellationToken);

        var candidate = candidates.FirstOrDefault();

        return candidate is null
            ? Result.Failure<OrderCandidate>(Error.NotFound("order.not_found", $"Order {orderId} was not found."))
            : Result.Success(candidate);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetOrderNumbersAsync(
        IEnumerable<Guid> orderIds,
        CancellationToken cancellationToken = default)
    {
        var ids = orderIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await dbContext.Set<Order>()
            .AsNoTracking()
            .Where(order => ids.Contains(order.Id))
            .Select(order => new { order.Id, order.OrderNumber })
            .ToDictionaryAsync(order => order.Id, order => order.OrderNumber, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> FindOrderIdsByNumberAsync(
        string search,
        CancellationToken cancellationToken = default)
    {
        var term = search.Trim().ToLowerInvariant();

        return await dbContext.Set<Order>()
            .AsNoTracking()
            .Where(order => order.OrderNumber.ToLower().Contains(term))
            .Select(order => order.Id)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Order> CandidateOrders()
        => dbContext.Set<Order>()
            .AsNoTracking()
            .Where(order => order.Status != OrderStatus.Cancelled);

    private static async Task<IReadOnlyList<OrderCandidate>> ToCandidatesAsync(
        IQueryable<Order> query,
        CancellationToken cancellationToken)
        => await query
            .OrderBy(order => order.CreatedAt)
            .Select(order => new OrderCandidate(
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
}
