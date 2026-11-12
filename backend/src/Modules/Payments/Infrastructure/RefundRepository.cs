using PaymentReconciliation.Modules.Payments.Application;
using PaymentReconciliation.Modules.Payments.Domain;
using PaymentReconciliation.Persistence.Abstractions;

namespace PaymentReconciliation.Modules.Payments.Infrastructure;

public sealed class RefundRepository(IAppDbContext dbContext) : IRefundRepository
{
    public void Add(Refund refund)
        => dbContext.Set<Refund>().Add(refund);
}
