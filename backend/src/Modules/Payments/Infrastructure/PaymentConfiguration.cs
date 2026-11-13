using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentReconciliation.Modules.Payments.Domain;

namespace PaymentReconciliation.Modules.Payments.Infrastructure;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments", "payments");

        builder.HasKey(payment => payment.Id);

        builder.Property(payment => payment.Provider)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(payment => payment.ProviderPaymentId)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(payment => new { payment.TenantId, payment.Provider, payment.ProviderPaymentId }).IsUnique();
        builder.HasIndex(payment => payment.OrderId);
        builder.HasIndex(payment => payment.ProviderOrderId);
        builder.HasIndex(payment => payment.Status);

        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.RefundedAmount).HasPrecision(18, 2);
        builder.Ignore(payment => payment.RefundableAmount);
        builder.Property(payment => payment.Currency).HasMaxLength(3).IsRequired();

        builder.Property(payment => payment.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(payment => payment.CustomerName).HasMaxLength(160);
        builder.Property(payment => payment.CustomerReference).HasMaxLength(64);
        builder.Property(payment => payment.Reference).HasMaxLength(64);
        builder.Property(payment => payment.FailureReason).HasMaxLength(400);

        builder.Ignore(payment => payment.DomainEvents);
    }
}
