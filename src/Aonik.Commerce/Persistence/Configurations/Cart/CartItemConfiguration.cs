using Aonik.Commerce.Entities.Cart;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Cart;

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity).IsRequired().HasPrecision(19, 4);
        builder.Property(x => x.UnitPriceSnapshot).IsRequired().HasPrecision(19, 4);
        builder.Property(x => x.Sku).HasMaxLength(64);
        builder.Property(x => x.NameSnapshot).HasMaxLength(256);

        builder.HasMany(x => x.Selections)
            .WithOne()
            .HasForeignKey(x => x.CartItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.CartId);
    }
}
