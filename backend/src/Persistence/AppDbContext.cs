using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Auditing.Domain;
using PaymentReconciliation.Modules.Auditing.Infrastructure;
using PaymentReconciliation.Modules.Identity.Domain;
using PaymentReconciliation.Modules.Orders.Domain;
using PaymentReconciliation.Modules.Orders.Infrastructure;
using PaymentReconciliation.Modules.Payments.Domain;
using PaymentReconciliation.Modules.Payments.Infrastructure;
using PaymentReconciliation.Modules.Reconciliation.Domain;
using PaymentReconciliation.Modules.Reconciliation.Infrastructure;
using PaymentReconciliation.Modules.Support.Domain;
using PaymentReconciliation.Modules.Support.Infrastructure;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.Modules.Tenancy.Infrastructure;
using PaymentReconciliation.Modules.Webhooks.Domain;
using PaymentReconciliation.Modules.Webhooks.Infrastructure;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;

namespace PaymentReconciliation.Persistence;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ITenantContext tenantContext,
    ITenantBoundaryBypass tenantBoundaryBypass)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IAppDbContext
{
    public ITenantContext TenantContext => tenantContext;

    public Guid? CurrentTenantId => tenantContext.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasSequence<long>("order_number_seq", "orders").StartsAt(1001);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WebhookEventConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReconciliationRecordConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditLogConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenantConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SupportTicketConfiguration).Assembly);
        modelBuilder.ApplyConfiguration(new ApplicationUserConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());

        ConfigureIdentityTables(modelBuilder);
        ConfigureTenantOwnership(modelBuilder);
        ConfigureCrossModuleRelationships(modelBuilder);
        ConfigureTenantFilters(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTenantBoundary();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyTenantBoundary();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private static void ConfigureIdentityTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>().ToTable("users", "identity");
        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("roles", "identity");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles", "identity");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims", "identity");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins", "identity");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens", "identity");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims", "identity");
    }

    private static void ConfigureTenantOwnership(ModelBuilder modelBuilder)
    {
        ConfigureTenantOwned<Order>(modelBuilder, hasAlternateKey: true);
        ConfigureTenantOwned<Payment>(modelBuilder, hasAlternateKey: true);
        ConfigureTenantOwned<Refund>(modelBuilder);
        ConfigureTenantOwned<WebhookEvent>(modelBuilder);
        ConfigureTenantOwned<ReconciliationRecord>(modelBuilder);
        ConfigureTenantOwned<AuditLog>(modelBuilder);
        ConfigureTenantOwned<Subscription>(modelBuilder);
        ConfigureTenantOwned<SubscriptionHistory>(modelBuilder);
        ConfigureTenantOwned<TenantInvitation>(modelBuilder);
        ConfigureTenantOwned<TenantWebhookSecret>(modelBuilder);
        ConfigureTenantOwned<SupportTicket>(modelBuilder, hasAlternateKey: true);
        ConfigureTenantOwned<SupportTicketMessage>(modelBuilder);
    }

    private static void ConfigureTenantOwned<TEntity>(
        ModelBuilder modelBuilder,
        bool hasAlternateKey = false)
        where TEntity : class, ITenantOwned
    {
        var builder = modelBuilder.Entity<TEntity>();

        builder.Property(entity => entity.TenantId).IsRequired().ValueGeneratedNever();
        builder.HasIndex(entity => entity.TenantId);

        var tableName = builder.Metadata.GetTableName() ?? builder.Metadata.ClrType.Name;
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(entity => entity.TenantId)
            .HasConstraintName($"fk_{tableName}_tenant_tenant_id")
            .OnDelete(DeleteBehavior.Restrict);

        if (hasAlternateKey)
        {
            builder.HasAlternateKey("TenantId", "Id");
        }
    }

    private static void ConfigureCrossModuleRelationships(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantUser>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .HasConstraintName("fk_tenant_users_asp_net_users_user_id")
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Payment>()
            .HasOne<Order>()
            .WithMany()
            .HasForeignKey(payment => new { payment.TenantId, payment.OrderId })
            .HasPrincipalKey(order => new { order.TenantId, order.Id })
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<WebhookEvent>()
            .HasOne<Payment>()
            .WithMany()
            .HasForeignKey(webhookEvent => new { webhookEvent.TenantId, webhookEvent.PaymentId })
            .HasPrincipalKey(payment => new { payment.TenantId, payment.Id })
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ReconciliationRecord>()
            .HasOne<Payment>()
            .WithMany()
            .HasForeignKey(record => new { record.TenantId, record.PaymentId })
            .HasPrincipalKey(payment => new { payment.TenantId, payment.Id })
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ReconciliationRecord>()
            .HasOne<Order>()
            .WithMany()
            .HasForeignKey(record => new { record.TenantId, record.OrderId })
            .HasPrincipalKey(order => new { order.TenantId, order.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureTenantFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>()
            .HasQueryFilter(order => CurrentTenantId != null && order.TenantId == CurrentTenantId);
        modelBuilder.Entity<Payment>()
            .HasQueryFilter(payment => CurrentTenantId != null && payment.TenantId == CurrentTenantId);
        modelBuilder.Entity<Refund>()
            .HasQueryFilter(refund => CurrentTenantId != null && refund.TenantId == CurrentTenantId);
        modelBuilder.Entity<WebhookEvent>()
            .HasQueryFilter(webhookEvent => CurrentTenantId != null && webhookEvent.TenantId == CurrentTenantId);
        modelBuilder.Entity<ReconciliationRecord>()
            .HasQueryFilter(record => CurrentTenantId != null && record.TenantId == CurrentTenantId);
        modelBuilder.Entity<AuditLog>()
            .HasQueryFilter(log => CurrentTenantId != null && log.TenantId == CurrentTenantId);
        modelBuilder.Entity<Subscription>()
            .HasQueryFilter(subscription => CurrentTenantId != null && subscription.TenantId == CurrentTenantId);
        modelBuilder.Entity<SubscriptionHistory>()
            .HasQueryFilter(history => CurrentTenantId != null && history.TenantId == CurrentTenantId);
        modelBuilder.Entity<TenantInvitation>()
            .HasQueryFilter(invitation => CurrentTenantId != null && invitation.TenantId == CurrentTenantId);
        modelBuilder.Entity<TenantWebhookSecret>()
            .HasQueryFilter(secret => CurrentTenantId != null && secret.TenantId == CurrentTenantId);
        modelBuilder.Entity<SupportTicket>()
            .HasQueryFilter(ticket => CurrentTenantId != null && ticket.TenantId == CurrentTenantId);
        modelBuilder.Entity<SupportTicketMessage>()
            .HasQueryFilter(message => CurrentTenantId != null && message.TenantId == CurrentTenantId);
    }

    private void ApplyTenantBoundary()
    {
        if (tenantBoundaryBypass.IsEnabled)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
        {
            var tenantIdProperty = entry.Property("TenantId");
            var currentTenantId = tenantContext.TenantId;

            if (entry.State == EntityState.Added)
            {
                var entityTenantId = (Guid)tenantIdProperty.CurrentValue!;
                if (entityTenantId == Guid.Empty)
                {
                    if (currentTenantId is null)
                    {
                        throw new InvalidOperationException(
                            "Cannot create tenant-owned data without a resolved tenant.");
                    }

                    tenantIdProperty.CurrentValue = currentTenantId.Value;
                }
                else if (currentTenantId is null || entityTenantId != currentTenantId.Value)
                {
                    throw new InvalidOperationException(
                        $"Tenant-owned {entry.Metadata.ClrType.Name} cannot be created for tenant {entityTenantId}; current tenant is {currentTenantId}.");
                }
            }
            else if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                var originalTenantId = (Guid)tenantIdProperty.OriginalValue!;
                var currentTenantIdValue = (Guid)tenantIdProperty.CurrentValue!;

                if (originalTenantId != currentTenantIdValue)
                {
                    throw new InvalidOperationException("A tenant-owned record cannot change tenant.");
                }

                if (currentTenantId is null || originalTenantId != currentTenantId.Value)
                {
                    throw new InvalidOperationException(
                        "Tenant-owned data belongs to a different tenant.");
                }
            }
        }
    }
}
