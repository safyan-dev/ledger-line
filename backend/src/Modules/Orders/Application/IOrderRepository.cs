using PaymentReconciliation.Modules.Orders.Domain;

namespace PaymentReconciliation.Modules.Orders.Application;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Order?> GetByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default);

    Task<Order?> GetByCustomerReferenceAsync(string customerReference, CancellationToken cancellationToken = default);

    Task LockForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(Order order);
}
