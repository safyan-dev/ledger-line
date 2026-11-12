using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentReconciliation.Modules.Payments.Domain;

namespace PaymentReconciliation.Modules.Payments.Infrastructure;

public sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("refunds", "payments");

        builder.HasKey(refund => refund.Id);

        builder.HasIndex(refund => refund.PaymentId);
        builder.HasIndex(refund => refund.CreatedAt);

        builder.Property(refund => refund.Amount).HasPrecision(18, 2);
        builder.Property(refund => refund.Currency).HasMaxLength(3).IsRequired();
        builder.Property(refund => refund.Reason).HasMaxLength(400).IsRequired();

        builder.Property(refund => refund.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(refund => refund.Provider).HasMaxLength(32).IsRequired();
        builder.Property(refund => refund.ProviderRefundId).HasMaxLength(64).IsRequired();
        builder.Property(refund => refund.CreatedBy).HasMaxLength(160).IsRequired();

        builder.Ignore(refund => refund.DomainEvents);
    }
}
