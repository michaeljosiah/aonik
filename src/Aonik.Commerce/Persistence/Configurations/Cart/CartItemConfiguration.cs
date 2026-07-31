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

        // Spec 068 §13 — non-null with a database default so rows that predate the column, and
        // generic-cart inserts that omit it, classify as BoxDish without a backfill. The default
        // is what R13 means here; it is NOT a null fallback, because the column rejects null.
        builder.Property(x => x.LineKind)
            .IsRequired()
            .HasMaxLength(16)
            .HasDefaultValue(CartLineKinds.BoxDish);

        // PersonalisationJson stays nvarchar(max): Spec 066 bounds neither group count nor
        // multi-select width, so a fixed cap could reject a valid canonical selection.
        builder.Property(x => x.PersonalisationSummary).HasMaxLength(512);
        builder.Property(x => x.PersonalisationAdjustment).HasPrecision(19, 4);
        builder.Property(x => x.UnitSurcharge).HasPrecision(19, 4);

        builder.HasMany(x => x.Selections)
            .WithOne()
            .HasForeignKey(x => x.CartItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.CartId);
    }
}
