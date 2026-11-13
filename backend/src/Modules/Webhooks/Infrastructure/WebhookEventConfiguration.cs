using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentReconciliation.Modules.Webhooks.Domain;

namespace PaymentReconciliation.Modules.Webhooks.Infrastructure;

public sealed class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
{
    public void Configure(EntityTypeBuilder<WebhookEvent> builder)
    {
        builder.ToTable("webhook_events", "webhooks");

        builder.HasKey(webhookEvent => webhookEvent.Id);

        builder.Property(webhookEvent => webhookEvent.Provider)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(webhookEvent => webhookEvent.ProviderEventId)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(webhookEvent => new { webhookEvent.TenantId, webhookEvent.Provider, webhookEvent.ProviderEventId })
            .IsUnique();

        builder.HasIndex(webhookEvent => webhookEvent.Status);
        builder.HasIndex(webhookEvent => webhookEvent.ReceivedAt);

        builder.Property(webhookEvent => webhookEvent.EventType)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(webhookEvent => webhookEvent.Payload)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(webhookEvent => webhookEvent.Signature)
            .HasMaxLength(256);

        builder.Property(webhookEvent => webhookEvent.PayloadHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(webhookEvent => webhookEvent.DuplicateCount)
            .HasDefaultValue(0);

        builder.Property(webhookEvent => webhookEvent.ConflictCount)
            .HasDefaultValue(0);

        builder.Property(webhookEvent => webhookEvent.LastConflictReason)
            .HasMaxLength(400);

        builder.Property(webhookEvent => webhookEvent.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(webhookEvent => webhookEvent.FailureReason)
            .HasMaxLength(400);

        builder.Ignore(webhookEvent => webhookEvent.DomainEvents);
    }
}
