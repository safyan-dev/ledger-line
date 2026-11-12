using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Orders.Application;
using PaymentReconciliation.Modules.Orders.Domain;
using PaymentReconciliation.Persistence.Abstractions;

namespace PaymentReconciliation.Modules.Orders.Infrastructure;

public sealed class OrderRepository(IAppDbContext dbContext) : IOrderRepository
{
    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tracked = dbContext.Set<Order>().Local.FirstOrDefault(order => order.Id == id);
        return tracked is not null
            ? Task.FromResult<Order?>(tracked)
            : dbContext.Set<Order>().FirstOrDefaultAsync(order => order.Id == id, cancellationToken);
    }

    public Task<Order?> GetByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
        => dbContext.Set<Order>().FirstOrDefaultAsync(order => order.OrderNumber == orderNumber, cancellationToken);

    public Task<Order?> GetByCustomerReferenceAsync(
        string customerReference,
        CancellationToken cancellationToken = default)
    {
        var normalized = customerReference.Trim().ToLowerInvariant();

        return dbContext.Set<Order>()
            .FirstOrDefaultAsync(
                order => order.CustomerReference != null && order.CustomerReference.ToLower() == normalized,
                cancellationToken);
    }

    public async Task LockForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM orders.orders WHERE id = {id} FOR UPDATE",
            cancellationToken);
    }

    public void Add(Order order) => dbContext.Set<Order>().Add(order);
}
