using Aonik.Commerce.Entities.Cart;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Cart;

public class OrderBundleSelectionConfiguration : IEntityTypeConfiguration<OrderBundleSelection>
{
    public void Configure(EntityTypeBuilder<OrderBundleSelection> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity).IsRequired().HasPrecision(19, 4);
        builder.Property(x => x.Sku).HasMaxLength(64);

        // Spec 068 §9 — kitchen-facing personalisation landing. Json columns stay nvarchar(max)
        // (unbounded canonical selections / envelope); scalars mirror the CartItem shapes.
        builder.Property(x => x.PersonalisationSummary).HasMaxLength(512);
        builder.Property(x => x.PersonalisationAdjustment).HasPrecision(19, 4);
        builder.Property(x => x.UnitSurcharge).HasPrecision(19, 4);

        builder.HasIndex(x => new { x.TenantId, x.OrderId });
    }
}
