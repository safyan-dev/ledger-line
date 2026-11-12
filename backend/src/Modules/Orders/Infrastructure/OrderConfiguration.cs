using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentReconciliation.Modules.Orders.Domain;

namespace PaymentReconciliation.Modules.Orders.Infrastructure;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", "orders");

        builder.HasKey(order => order.Id);

        builder.Property(order => order.OrderNumber)
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(order => order.OrderNumber).IsUnique();

        builder.Property(order => order.CustomerName)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(order => order.CustomerReference)
            .HasMaxLength(64);

        builder.HasIndex(order => order.CustomerReference);

        builder.Property(order => order.Description)
            .HasMaxLength(400);

        builder.Property(order => order.ExpectedAmount)
            .HasPrecision(18, 2);

        builder.Property(order => order.PaidAmount)
            .HasPrecision(18, 2);

        builder.Property(order => order.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(order => order.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Ignore(order => order.DomainEvents);
    }
}
