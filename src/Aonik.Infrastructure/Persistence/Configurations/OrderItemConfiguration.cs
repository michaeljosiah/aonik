using Aonik.Domain.Orders.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ItemType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Reference)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Amount)
            .IsRequired()
            .HasPrecision(19, 4);

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.HasIndex(x => x.OrderId);
        builder.HasIndex(x => x.ItemType);
    }
}
