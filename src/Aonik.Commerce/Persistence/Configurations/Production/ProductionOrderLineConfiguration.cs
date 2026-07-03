using Aonik.Commerce.Entities.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Production;

public class ProductionOrderLineConfiguration : IEntityTypeConfiguration<ProductionOrderLine>
{
    public void Configure(EntityTypeBuilder<ProductionOrderLine> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PlannedQuantity).IsRequired().HasPrecision(19, 4);
        builder.Property(x => x.ProducedQuantity).HasPrecision(19, 4);
        // The frozen per-portion component bill (Spec 056 §7) — JSON, mirroring the house
        // precedent for JSON columns (Product.TagsJson/AttributesJson, ProductVariant.OptionsJson).
        builder.Property(x => x.RecipeSnapshotJson).IsRequired().HasColumnType("nvarchar(max)");

        builder.HasIndex(x => new { x.TenantId, x.ProductionOrderId });
    }
}
