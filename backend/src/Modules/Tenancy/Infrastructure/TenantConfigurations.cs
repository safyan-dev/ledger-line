using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentReconciliation.Modules.Tenancy.Domain;

namespace PaymentReconciliation.Modules.Tenancy.Infrastructure;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants", "tenancy");
        builder.HasKey(tenant => tenant.Id);
        builder.Property(tenant => tenant.Name).HasMaxLength(160).IsRequired();
        builder.Property(tenant => tenant.Slug).HasMaxLength(40).IsRequired();
        builder.HasIndex(tenant => tenant.Slug).IsUnique();
        builder.Property(tenant => tenant.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(tenant => tenant.Status);
        builder.Property(tenant => tenant.BusinessContactEmail).HasMaxLength(320);
        builder.Property(tenant => tenant.DefaultCurrency).HasMaxLength(3).IsRequired().HasDefaultValue("USD");
        builder.Property(tenant => tenant.TimeZone).HasMaxLength(64).IsRequired().HasDefaultValue("UTC");
        builder.Ignore(tenant => tenant.DomainEvents);
    }
}

public sealed class TenantUserConfiguration : IEntityTypeConfiguration<TenantUser>
{
    public void Configure(EntityTypeBuilder<TenantUser> builder)
    {
        builder.ToTable("tenant_users", "tenancy");
        builder.HasKey(membership => membership.Id);
        builder.Property(membership => membership.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(membership => new { membership.TenantId, membership.UserId }).IsUnique();
        builder.HasIndex(membership => membership.UserId);
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(membership => membership.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Ignore(membership => membership.DomainEvents);
    }
}
