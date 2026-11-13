namespace PaymentReconciliation.Modules.Orders.Infrastructure;

using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Orders.Contracts;
using PaymentReconciliation.Modules.Orders.Domain;
using PaymentReconciliation.Persistence.Abstractions;

public sealed class OrderReporting(IAppDbContext dbContext) : IOrderReporting
{
    public Task<int> CountAsync(CancellationToken cancellationToken = default)
        => dbContext.Set<Order>().AsNoTracking().CountAsync(cancellationToken);
}
