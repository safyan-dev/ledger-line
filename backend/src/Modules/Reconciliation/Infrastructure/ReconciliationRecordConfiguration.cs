using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentReconciliation.Modules.Reconciliation.Domain;

namespace PaymentReconciliation.Modules.Reconciliation.Infrastructure;

public sealed class ReconciliationRecordConfiguration : IEntityTypeConfiguration<ReconciliationRecord>
{
    public void Configure(EntityTypeBuilder<ReconciliationRecord> builder)
    {
        builder.ToTable("reconciliation_records", "reconciliation");

        builder.HasKey(record => record.Id);

        builder.HasIndex(record => record.PaymentId);

        builder.HasIndex(record => new { record.TenantId, record.PaymentId })
            .IsUnique()
            .HasFilter("\"outcome\" IN ('Matched', 'PartiallyMatched', 'ManuallyMatched')")
            .HasDatabaseName("ux_reconciliation_records_payment_success");
        builder.HasIndex(record => record.OrderId);
        builder.HasIndex(record => record.Outcome);
        builder.HasIndex(record => record.CreatedAt);

        builder.Property(record => record.Outcome)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(record => record.Strategy)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(record => record.ExpectedAmount).HasPrecision(18, 2);
        builder.Property(record => record.ActualAmount).HasPrecision(18, 2);
        builder.Property(record => record.Difference).HasPrecision(18, 2);

        builder.Property(record => record.Currency).HasMaxLength(3).IsRequired();
        builder.Property(record => record.Reason).HasMaxLength(400);
        builder.Property(record => record.Notes).HasMaxLength(400);

        builder.Property(record => record.MatchedBy)
            .HasMaxLength(160)
            .IsRequired();

        builder.Ignore(record => record.DomainEvents);
    }
}
