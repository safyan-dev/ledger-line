using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentReconciliation.Modules.Support.Domain;

namespace PaymentReconciliation.Modules.Support.Infrastructure;

public sealed class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> builder)
    {
        builder.ToTable("support_tickets", "support");
        builder.HasKey(ticket => ticket.Id);
        builder.Property(ticket => ticket.Subject).HasMaxLength(200).IsRequired();
        builder.Property(ticket => ticket.Description).HasMaxLength(4000).IsRequired();
        builder.Property(ticket => ticket.Category).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(ticket => ticket.Priority).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(ticket => ticket.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(ticket => ticket.CreatedByName).HasMaxLength(160).IsRequired();
        builder.HasIndex(ticket => new { ticket.TenantId, ticket.Status });
        builder.HasIndex(ticket => new { ticket.TenantId, ticket.CreatedAt });
        builder.Ignore(ticket => ticket.DomainEvents);
    }
}

public sealed class SupportTicketMessageConfiguration : IEntityTypeConfiguration<SupportTicketMessage>
{
    public void Configure(EntityTypeBuilder<SupportTicketMessage> builder)
    {
        builder.ToTable("support_ticket_messages", "support");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.AuthorName).HasMaxLength(160).IsRequired();
        builder.Property(message => message.AuthorRole).HasMaxLength(32).IsRequired();
        builder.Property(message => message.Body).HasMaxLength(4000).IsRequired();
        builder.HasIndex(message => new { message.TenantId, message.TicketId, message.CreatedAt });
        builder.HasOne<SupportTicket>()
            .WithMany()
            .HasForeignKey(message => new { message.TenantId, message.TicketId })
            .HasPrincipalKey(ticket => new { ticket.TenantId, ticket.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
