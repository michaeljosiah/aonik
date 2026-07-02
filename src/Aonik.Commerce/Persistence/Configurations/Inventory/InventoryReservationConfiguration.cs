using Aonik.Commerce.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Inventory;

public class InventoryReservationConfiguration : IEntityTypeConfiguration<InventoryReservation>
{
    public void Configure(EntityTypeBuilder<InventoryReservation> builder)
    {
        // A hold holds EXACTLY one stock item — the same exactly-one CHECK as the level, so an
        // ingredient hold has an identity the commit/release/sweep lookups can resolve (Spec 052 §8).
        builder.ToTable("InventoryReservations", t => t.HasCheckConstraint(
            "CK_InventoryReservations_ExactlyOneStockItem",
            "([ProductVariantId] IS NOT NULL AND [IngredientId] IS NULL AND [StockItemKind] = N'ProductVariant') " +
            "OR ([ProductVariantId] IS NULL AND [IngredientId] IS NOT NULL AND [StockItemKind] = N'Ingredient')"));

        builder.HasKey(x => x.Id);

        // DB default backfills the discriminator for existing (variant-keyed) rows in the Spec 052
        // migration, mirroring the level.
        builder.Property(x => x.StockItemKind)
            .IsRequired()
            .HasMaxLength(16)
            .HasDefaultValue(StockItemKinds.ProductVariant);

        // HoldRef generalizes the Spec 042 CartId (a production hold has no cart, Spec 052 §8). It
        // stays mapped onto the legacy CartId column: EF migrations cannot scaffold a data-preserving
        // RenameColumn from a property rename, and hand-editing migrations is prohibited.
        builder.Property(x => x.HoldRef).HasColumnName("CartId");

        builder.Property(x => x.Quantity).IsRequired().HasPrecision(19, 4);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(16);

        builder.HasIndex(x => new { x.TenantId, x.HoldRef });
        builder.HasIndex(x => x.ProductVariantId);
        builder.HasIndex(x => x.IngredientId);
        builder.HasIndex(x => new { x.Status, x.ExpiresAt });
    }
}
