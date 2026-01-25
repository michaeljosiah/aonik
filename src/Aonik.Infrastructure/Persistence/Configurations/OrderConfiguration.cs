using Aonik.Domain.Orders.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.CurrencyIn)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.CurrencyOut)
            .HasMaxLength(3);

        builder.Property(x => x.AmountIn)
            .HasPrecision(19, 4);

        builder.Property(x => x.AmountOut)
            .HasPrecision(19, 4);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.IdempotencyKey)
            .HasMaxLength(200);

        builder.Property(x => x.PurposeCode)
            .HasMaxLength(50);

        builder.Property(x => x.OriginCountry)
            .HasMaxLength(2);

        builder.Property(x => x.DestinationCountry)
            .HasMaxLength(2);

        builder.Property(x => x.FeesJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.ProvenanceJson)
            .HasColumnType("nvarchar(max)");

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.PartyRoles)
            .WithOne()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.HistoryEvents)
            .WithOne()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.OrderType);
        builder.HasIndex(x => x.PayerPartyId);
        builder.HasIndex(x => x.IdempotencyKey);
    }
}
