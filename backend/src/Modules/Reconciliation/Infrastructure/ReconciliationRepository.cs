using PaymentReconciliation.Modules.Reconciliation.Application;
using PaymentReconciliation.Modules.Reconciliation.Domain;
using PaymentReconciliation.Persistence.Abstractions;

namespace PaymentReconciliation.Modules.Reconciliation.Infrastructure;

public sealed class ReconciliationRepository(IAppDbContext dbContext) : IReconciliationRepository
{
    public void Add(ReconciliationRecord record)
        => dbContext.Set<ReconciliationRecord>().Add(record);
}
