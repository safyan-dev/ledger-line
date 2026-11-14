using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentReconciliation.Modules.Tenancy.Domain;

namespace PaymentReconciliation.Modules.Tenancy.Infrastructure;

public sealed class TenantInvitationConfiguration : IEntityTypeConfiguration<TenantInvitation>
{
    public void Configure(EntityTypeBuilder<TenantInvitation> builder)
    {
        builder.ToTable("tenant_invitations", "tenancy");
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.Email).HasMaxLength(320).IsRequired();
        builder.Property(invitation => invitation.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(invitation => invitation.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(invitation => invitation.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(invitation => invitation.TokenHash).IsUnique();
        builder.HasIndex(invitation => invitation.ExpiresAt);
        builder.HasIndex(invitation => new { invitation.TenantId, invitation.Status });
        builder.HasIndex(invitation => new { invitation.TenantId, invitation.Email })
            .IsUnique()
            .HasFilter("status = 'Pending'");
        builder.Ignore(invitation => invitation.DomainEvents);
    }
}
