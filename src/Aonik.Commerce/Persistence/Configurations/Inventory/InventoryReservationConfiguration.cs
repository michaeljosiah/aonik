using Aonik.Commerce.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Inventory;

public class InventoryReservationConfiguration : IEntityTypeConfiguration<InventoryReservation>
{
    public void Configure(EntityTypeBuilder<InventoryReservation> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity).IsRequired().HasPrecision(19, 4);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(16);

        builder.HasIndex(x => new { x.TenantId, x.CartId });
        builder.HasIndex(x => x.ProductVariantId);
        builder.HasIndex(x => new { x.Status, x.ExpiresAt });
    }
}
