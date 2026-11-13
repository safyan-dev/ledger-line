using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentReconciliation.Modules.Tenancy.Domain;

namespace PaymentReconciliation.Modules.Tenancy.Infrastructure;

public sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("plans", "tenancy");
        builder.HasKey(plan => plan.Id);
        builder.Property(plan => plan.Code).HasMaxLength(32).IsRequired();
        builder.HasIndex(plan => plan.Code).IsUnique();
        builder.Property(plan => plan.Name).HasMaxLength(120).IsRequired();
        builder.Property(plan => plan.Tagline).HasMaxLength(240).IsRequired();
        builder.Property(plan => plan.Audience).HasMaxLength(160).IsRequired();
        builder.Property(plan => plan.MonthlyPrice).HasPrecision(18, 2);
        builder.Property(plan => plan.PriceNote).HasMaxLength(60).IsRequired();
        builder.Property(plan => plan.SortOrder).IsRequired();
        builder.HasIndex(plan => plan.SortOrder);
        builder.Ignore(plan => plan.IsFree);
    }
}

public sealed class FeatureConfiguration : IEntityTypeConfiguration<Feature>
{
    public void Configure(EntityTypeBuilder<Feature> builder)
    {
        builder.ToTable("features", "tenancy");
        builder.HasKey(feature => feature.Id);
        builder.Property(feature => feature.Key).HasMaxLength(64).IsRequired();
        builder.HasIndex(feature => feature.Key).IsUnique();
        builder.Property(feature => feature.Name).HasMaxLength(120).IsRequired();
        builder.Property(feature => feature.Description).HasMaxLength(400).IsRequired();
    }
}

public sealed class PlanFeatureConfiguration : IEntityTypeConfiguration<PlanFeature>
{
    public void Configure(EntityTypeBuilder<PlanFeature> builder)
    {
        builder.ToTable("plan_features", "tenancy");
        builder.HasKey(planFeature => planFeature.Id);
        builder.HasIndex(planFeature => new { planFeature.PlanId, planFeature.FeatureId }).IsUnique();
        builder.Property(planFeature => planFeature.Note).HasMaxLength(200);
        builder.HasOne<Plan>()
            .WithMany(plan => plan.Features)
            .HasForeignKey(planFeature => planFeature.PlanId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(planFeature => planFeature.Feature)
            .WithMany()
            .HasForeignKey(planFeature => planFeature.FeatureId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PlanLimitConfiguration : IEntityTypeConfiguration<PlanLimit>
{
    public void Configure(EntityTypeBuilder<PlanLimit> builder)
    {
        builder.ToTable("plan_limits", "tenancy");
        builder.HasKey(limit => limit.Id);
        builder.HasIndex(limit => new { limit.PlanId, limit.Key }).IsUnique();
        builder.Property(limit => limit.Key).HasMaxLength(64).IsRequired();
        builder.Property(limit => limit.Label).HasMaxLength(120).IsRequired();
        builder.Property(limit => limit.Display).HasMaxLength(80).IsRequired();
        builder.HasOne<Plan>()
            .WithMany(plan => plan.Limits)
            .HasForeignKey(limit => limit.PlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscriptions", "tenancy");
        builder.HasKey(subscription => subscription.Id);
        builder.HasIndex(subscription => subscription.TenantId).IsUnique();
        builder.Property(subscription => subscription.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(subscription => subscription.ActivationSource).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(subscription => subscription.PaymentConfirmed).IsRequired();
        builder.Property(subscription => subscription.CancelAtPeriodEnd).IsRequired();
        builder.HasIndex(subscription => new { subscription.Status, subscription.CurrentPeriodEnd });
        builder.HasOne<Plan>()
            .WithMany()
            .HasForeignKey(subscription => subscription.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(subscription => subscription.DomainEvents);
        builder.Ignore(subscription => subscription.IsActive);
    }
}

public sealed class SubscriptionHistoryConfiguration : IEntityTypeConfiguration<SubscriptionHistory>
{
    public void Configure(EntityTypeBuilder<SubscriptionHistory> builder)
    {
        builder.ToTable("subscription_history", "tenancy");
        builder.HasKey(history => history.Id);
        builder.Property(history => history.Event).HasMaxLength(64).IsRequired();
        builder.Property(history => history.Status).HasMaxLength(32).IsRequired();
        builder.Property(history => history.Note).HasMaxLength(400);
        builder.Property(history => history.IdempotencyKey).HasMaxLength(80);
        builder.HasIndex(history => new { history.TenantId, history.OccurredAt });
        builder.HasIndex(history => new { history.TenantId, history.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"idempotency_key\" IS NOT NULL");
        builder.HasOne<Subscription>()
            .WithMany()
            .HasForeignKey(history => history.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
