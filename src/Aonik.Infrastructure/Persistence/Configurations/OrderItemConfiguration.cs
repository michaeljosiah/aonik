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

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.CurrencyIn)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.CurrencyOut)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.AmountIn)
            .HasPrecision(19, 4);

        builder.Property(x => x.AmountOut)
            .HasPrecision(19, 4);

        builder.Property(x => x.FeesTotal)
            .HasPrecision(19, 4);

        builder.Property(x => x.DetailsJson)
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(x => x.OrderId);
        builder.HasIndex(x => x.ReceiverPartyId);
        builder.HasIndex(x => x.PricingQuoteId);
        builder.HasIndex(x => new { x.OrderId, x.ItemIndex });
    }
}
