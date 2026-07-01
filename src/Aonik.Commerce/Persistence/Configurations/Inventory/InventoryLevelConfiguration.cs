using Aonik.Commerce.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Inventory;

public class InventoryLevelConfiguration : IEntityTypeConfiguration<InventoryLevel>
{
    public void Configure(EntityTypeBuilder<InventoryLevel> builder)
    {
        // A level stocks EXACTLY one stock item — a variant or an ingredient, agreeing with the
        // discriminator — enforced at the database, not just the service (Spec 052 §8). The later
        // MapTable call overrides the table name to the Ank-prefixed form; the check constraint
        // survives (OrderFulfilmentRef precedent).
        builder.ToTable("InventoryLevels", t => t.HasCheckConstraint(
            "CK_InventoryLevels_ExactlyOneStockItem",
            "([ProductVariantId] IS NOT NULL AND [IngredientId] IS NULL AND [StockItemKind] = N'ProductVariant') " +
            "OR ([ProductVariantId] IS NULL AND [IngredientId] IS NOT NULL AND [StockItemKind] = N'Ingredient')"));

        builder.HasKey(x => x.Id);

        // DB default backfills the discriminator for existing finished-goods rows in the Spec 052
        // migration (every legacy row has a non-null ProductVariantId) and keeps raw inserts valid.
        builder.Property(x => x.StockItemKind)
            .IsRequired()
            .HasMaxLength(16)
            .HasDefaultValue(StockItemKinds.ProductVariant);

        builder.Property(x => x.Location).HasMaxLength(128);
        builder.Property(x => x.OnHand).IsRequired().HasPrecision(19, 4);
        builder.Property(x => x.Reserved).IsRequired().HasPrecision(19, 4);
        builder.Property(x => x.ReorderPoint).HasPrecision(19, 4);
        builder.Property(x => x.ReorderQuantity).HasPrecision(19, 4);

        // One level row per (tenant, stock item, location), unique within each kind (Spec 052 §8).
        // Location NULL = the default location; for variants it stays outside the DB uniqueness
        // scope exactly as before Spec 052 (the pre-052 unique index carried the EF SQL Server
        // convention filter [Location] IS NOT NULL) — the service's get-or-create guards it.
        builder.HasIndex(x => new { x.TenantId, x.ProductVariantId, x.Location })
            .IsUnique()
            .HasFilter("[ProductVariantId] IS NOT NULL AND [Location] IS NOT NULL");
        builder.HasIndex(x => new { x.TenantId, x.IngredientId, x.Location })
            .IsUnique()
            .HasFilter("[IngredientId] IS NOT NULL AND [Location] IS NOT NULL");

        // Backstop for the ingredient DEFAULT location (Spec 052 §8): every ingredient admin path
        // addresses the Location == NULL row, so two concurrent GetOrCreateDefaultLevelAsync misses
        // must not be able to seed duplicate default rows. SQL-only — InMemory cannot enforce it;
        // the service's get-or-create remains the single-row-by-construction path.
        builder.HasIndex(x => new { x.TenantId, x.IngredientId })
            .IsUnique()
            .HasFilter("[IngredientId] IS NOT NULL AND [Location] IS NULL");
    }
}
