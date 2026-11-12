using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Payments.Application;
using PaymentReconciliation.Modules.Payments.Domain;
using PaymentReconciliation.Persistence.Abstractions;

namespace PaymentReconciliation.Modules.Payments.Infrastructure;

public sealed class PaymentRepository(IAppDbContext dbContext) : IPaymentRepository
{
    public Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tracked = dbContext.Set<Payment>().Local.FirstOrDefault(payment => payment.Id == id);
        return tracked is not null
            ? Task.FromResult<Payment?>(tracked)
            : dbContext.Set<Payment>().FirstOrDefaultAsync(payment => payment.Id == id, cancellationToken);
    }

    public Task<Payment?> GetByProviderPaymentIdAsync(
        string provider,
        string providerPaymentId,
        CancellationToken cancellationToken = default)
        => dbContext.Set<Payment>().FirstOrDefaultAsync(
            payment => payment.Provider == provider && payment.ProviderPaymentId == providerPaymentId,
            cancellationToken);

    public async Task LockForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM payments.payments WHERE id = {id} FOR UPDATE",
            cancellationToken);
    }

    public void Add(Payment payment)
        => dbContext.Set<Payment>().Add(payment);
}
