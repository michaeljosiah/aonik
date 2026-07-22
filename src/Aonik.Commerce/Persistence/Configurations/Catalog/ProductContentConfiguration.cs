using Aonik.Commerce.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Catalog;

public class ProductContentConfiguration : IEntityTypeConfiguration<ProductContent>
{
    public void Configure(EntityTypeBuilder<ProductContent> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ServingLabel).IsRequired().HasMaxLength(128);
        builder.Property(x => x.HeatingJson).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(x => x.Ingredients).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Allergens).HasColumnType("nvarchar(max)");

        // Spec 066 bounds neither group count nor multi-select width, so a fixed cap could reject
        // a valid canonical selection at persistence after it passed validation (§10).
        builder.Property(x => x.DescribesSelectionJson).IsRequired().HasColumnType("nvarchar(max)");

        ConfigureFigures(builder);

        // At most one default block per product.
        builder.HasIndex(x => new { x.TenantId, x.ProductId }).IsUnique();
    }

    /// <summary>Figures are decimal(9,2) — service rule V-C7 rejects negatives, non-finite values
    /// and column-bound overflow so SQL never stores −500 kcal or 500s on precision.</summary>
    private static void ConfigureFigures(EntityTypeBuilder<ProductContent> builder)
    {
        builder.Property(x => x.Kcal).HasPrecision(9, 2);
        builder.Property(x => x.ProteinGrams).HasPrecision(9, 2);
        builder.Property(x => x.CarbsGrams).HasPrecision(9, 2);
        builder.Property(x => x.FatGrams).HasPrecision(9, 2);
        builder.Property(x => x.FibreGrams).HasPrecision(9, 2);
        builder.Property(x => x.SugarsGrams).HasPrecision(9, 2);
        builder.Property(x => x.SaltGrams).HasPrecision(9, 2);
    }
}
