using Aonik.Commerce.Entities.Sourcing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Sourcing;

public class GoodsReceiptLineConfiguration : IEntityTypeConfiguration<GoodsReceiptLine>
{
    public void Configure(EntityTypeBuilder<GoodsReceiptLine> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.QuantityReceived).IsRequired().HasPrecision(19, 4);
        builder.Property(x => x.UnitCostActual).HasPrecision(19, 4);
        builder.Property(x => x.Currency).HasMaxLength(3);

        // Lines of one receipt.
        builder.HasIndex(x => new { x.TenantId, x.GoodsReceiptId });

        // Per-ingredient receipt history — cumulative received sums (Spec 054 §9).
        builder.HasIndex(x => new { x.TenantId, x.IngredientId });
    }
}
