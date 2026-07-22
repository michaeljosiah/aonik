using Aonik.Commerce.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Catalog;

public class ProductContentVariantConfiguration : IEntityTypeConfiguration<ProductContentVariant>
{
    public void Configure(EntityTypeBuilder<ProductContentVariant> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ServingLabel).IsRequired().HasMaxLength(128);
        builder.Property(x => x.SelectionJson).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(x => x.Ingredients).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Allergens).HasColumnType("nvarchar(max)");
        builder.Property(x => x.HeatingJson).HasColumnType("nvarchar(max)");

        // SHA-256 hex — always exactly 64 chars; the ONLY selection representation ever indexed.
        builder.Property(x => x.SelectionHash).IsRequired().HasMaxLength(64).IsFixedLength();

        builder.Property(x => x.Kcal).HasPrecision(9, 2);
        builder.Property(x => x.ProteinGrams).HasPrecision(9, 2);
        builder.Property(x => x.CarbsGrams).HasPrecision(9, 2);
        builder.Property(x => x.FatGrams).HasPrecision(9, 2);
        builder.Property(x => x.FibreGrams).HasPrecision(9, 2);
        builder.Property(x => x.SugarsGrams).HasPrecision(9, 2);
        builder.Property(x => x.SaltGrams).HasPrecision(9, 2);

        // One row per (product, combination) — active OR retired: variants soft-retire via
        // IsActive (V-C5), and re-authoring a retired combination reactivates that row rather
        // than inserting a duplicate the index would reject.
        builder.HasIndex(x => new { x.TenantId, x.ProductId, x.SelectionHash }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ProductId }, "IX_AnkProductContentVariants_TenantId_ProductId");
    }
}
