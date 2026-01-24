using Aonik.Domain.Orders.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderNumber)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.OrderType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ServiceCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.AmountIn)
            .IsRequired()
            .HasPrecision(19, 4);

        builder.Property(x => x.CurrencyIn)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.AmountOut)
            .HasPrecision(19, 4);

        builder.Property(x => x.CurrencyOut)
            .HasMaxLength(3);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.OrderDetailsJson)
            .IsRequired();

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.OrderNumber)
            .IsUnique();

        builder.HasIndex(x => x.OrderType);
        builder.HasIndex(x => x.ServiceCode);
        builder.HasIndex(x => x.InvoiceId);
        builder.HasIndex(x => x.Status);
    }
}
