using PaymentReconciliation.Modules.Reconciliation.Domain;

namespace PaymentReconciliation.Modules.Reconciliation.Application;

public interface IReconciliationRepository
{
    void Add(ReconciliationRecord record);
}
