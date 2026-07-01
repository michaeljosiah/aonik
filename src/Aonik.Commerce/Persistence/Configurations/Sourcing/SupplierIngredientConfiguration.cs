using Aonik.Commerce.Entities.Sourcing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Sourcing;

public class SupplierIngredientConfiguration : IEntityTypeConfiguration<SupplierIngredient>
{
    public void Configure(EntityTypeBuilder<SupplierIngredient> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Sku).HasMaxLength(64);
        builder.Property(x => x.PackSize).IsRequired().HasPrecision(19, 4);
        builder.Property(x => x.PackPrice).IsRequired().HasPrecision(19, 4);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);

        // One price-list row per (supplier, ingredient) — the upsert key (Spec 053 §9). The
        // service pre-checks; the index guards SQL Server under concurrency.
        builder.HasIndex(x => new { x.TenantId, x.SupplierId, x.IngredientId }).IsUnique();

        // "Who supplies this ingredient?" lookups — the shortfall-seed and supplier-choice reads.
        builder.HasIndex(x => new { x.TenantId, x.IngredientId });
    }
}
