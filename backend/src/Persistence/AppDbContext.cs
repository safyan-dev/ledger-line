using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Auditing.Domain;
using PaymentReconciliation.Modules.Auditing.Infrastructure;
using PaymentReconciliation.Modules.Orders.Domain;
using PaymentReconciliation.Modules.Orders.Infrastructure;
using PaymentReconciliation.Modules.Payments.Domain;
using PaymentReconciliation.Modules.Payments.Infrastructure;
using PaymentReconciliation.Modules.Reconciliation.Domain;
using PaymentReconciliation.Modules.Reconciliation.Infrastructure;
using PaymentReconciliation.Modules.Webhooks.Domain;
using PaymentReconciliation.Modules.Webhooks.Infrastructure;
using PaymentReconciliation.Persistence.Abstractions;

namespace PaymentReconciliation.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<long>("order_number_seq", "orders").StartsAt(1001);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WebhookEventConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReconciliationRecordConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditLogConfiguration).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
