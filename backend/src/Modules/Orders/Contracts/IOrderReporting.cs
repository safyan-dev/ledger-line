namespace PaymentReconciliation.Modules.Orders.Contracts;

public interface IOrderReporting
{
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
