using PaymentReconciliation.Modules.Payments.Domain;

namespace PaymentReconciliation.Modules.Payments.Application;

public interface IRefundRepository
{
    void Add(Refund refund);
}
