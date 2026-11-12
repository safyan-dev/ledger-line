using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentReconciliation.Modules.Auditing.Domain;

namespace PaymentReconciliation.Modules.Auditing.Infrastructure;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs", "audit");

        builder.HasKey(log => log.Id);

        builder.HasIndex(log => log.OccurredAt);
        builder.HasIndex(log => new { log.EntityType, log.EntityId });
        builder.HasIndex(log => log.Module);
        builder.HasIndex(log => log.Action);

        builder.Property(log => log.ActorName).HasMaxLength(160).IsRequired();
        builder.Property(log => log.ActorRole).HasMaxLength(32).IsRequired();
        builder.Property(log => log.Action).HasMaxLength(80).IsRequired();
        builder.Property(log => log.Module).HasMaxLength(40).IsRequired();
        builder.Property(log => log.EntityType).HasMaxLength(64).IsRequired();
        builder.Property(log => log.Summary).HasMaxLength(400).IsRequired();
        builder.Property(log => log.Metadata).HasColumnType("jsonb");
        builder.Property(log => log.CorrelationId).HasMaxLength(64);
    }
}

public sealed class ActorConfiguration : IEntityTypeConfiguration<Actor>
{
    public void Configure(EntityTypeBuilder<Actor> builder)
    {
        builder.ToTable("actors", "audit");

        builder.HasKey(actor => actor.Id);

        builder.Property(actor => actor.DisplayName).HasMaxLength(160).IsRequired();
        builder.Property(actor => actor.Email).HasMaxLength(200).IsRequired();
        builder.HasIndex(actor => actor.Email).IsUnique();
        builder.Property(actor => actor.Role).HasMaxLength(32).IsRequired();
    }
}
