using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentReconciliation.Modules.Auditing.Domain;

namespace PaymentReconciliation.Modules.Auditing.Infrastructure;

public sealed class PlatformAuditLogConfiguration : IEntityTypeConfiguration<PlatformAuditLog>
{
    public void Configure(EntityTypeBuilder<PlatformAuditLog> builder)
    {
        builder.ToTable("platform_audit_logs", "audit");

        builder.HasKey(log => log.Id);

        builder.HasIndex(log => log.OccurredAt);
        builder.HasIndex(log => log.ActorId);
        builder.HasIndex(log => log.TenantId);
        builder.HasIndex(log => log.Action);
        builder.HasIndex(log => new { log.TargetType, log.TargetId });

        builder.Property(log => log.ActorName).HasMaxLength(160).IsRequired();
        builder.Property(log => log.ActorEmail).HasMaxLength(320);
        builder.Property(log => log.ActorRole).HasMaxLength(32).IsRequired();
        builder.Property(log => log.Action).HasMaxLength(100).IsRequired();
        builder.Property(log => log.TargetType).HasMaxLength(64).IsRequired();
        builder.Property(log => log.Summary).HasMaxLength(500).IsRequired();
        builder.Property(log => log.Metadata).HasColumnType("jsonb");
        builder.Property(log => log.CorrelationId).HasMaxLength(64);
    }
}
