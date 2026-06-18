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

        builder.HasIndex(x => new { x.TenantId, x.OrderId });
    }
}
