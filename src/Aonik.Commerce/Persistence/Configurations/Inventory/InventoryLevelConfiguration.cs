using Aonik.Commerce.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Inventory;

public class InventoryLevelConfiguration : IEntityTypeConfiguration<InventoryLevel>
{
    public void Configure(EntityTypeBuilder<InventoryLevel> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Location).HasMaxLength(128);
        builder.Property(x => x.OnHand).IsRequired().HasPrecision(19, 4);
        builder.Property(x => x.Reserved).IsRequired().HasPrecision(19, 4);

        // One level row per (tenant, variant, location). Location NULL = the default location.
        builder.HasIndex(x => new { x.TenantId, x.ProductVariantId, x.Location }).IsUnique();
    }
}
