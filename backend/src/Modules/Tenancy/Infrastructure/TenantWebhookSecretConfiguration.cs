using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentReconciliation.Modules.Tenancy.Domain;

namespace PaymentReconciliation.Modules.Tenancy.Infrastructure;

public sealed class TenantWebhookSecretConfiguration : IEntityTypeConfiguration<TenantWebhookSecret>
{
    public void Configure(EntityTypeBuilder<TenantWebhookSecret> builder)
    {
        builder.ToTable("tenant_webhook_secrets", "tenancy");

        builder.HasKey(secret => secret.Id);
        builder.Property(secret => secret.Provider).HasMaxLength(32).IsRequired();
        builder.Property(secret => secret.ProtectedSecret).HasMaxLength(2048).IsRequired();
        builder.HasIndex(secret => new { secret.TenantId, secret.Provider }).IsUnique();
    }
}
