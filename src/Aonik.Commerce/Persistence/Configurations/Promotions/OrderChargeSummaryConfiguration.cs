using Aonik.Commerce.Entities.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Promotions;

public class OrderChargeSummaryConfiguration : IEntityTypeConfiguration<OrderChargeSummary>
{
    public void Configure(EntityTypeBuilder<OrderChargeSummary> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Subtotal).HasPrecision(19, 4);
        builder.Property(x => x.DiscountTotal).HasPrecision(19, 4);
        builder.Property(x => x.TaxTotal).HasPrecision(19, 4);
        builder.Property(x => x.Total).HasPrecision(19, 4);
        builder.Property(x => x.DiscountCode).HasMaxLength(64);

        builder.HasIndex(x => new { x.TenantId, x.OrderId });
    }
}
