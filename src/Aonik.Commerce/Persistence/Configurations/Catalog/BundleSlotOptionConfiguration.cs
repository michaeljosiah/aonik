using Aonik.Commerce.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Catalog;

public class BundleSlotOptionConfiguration : IEntityTypeConfiguration<BundleSlotOption>
{
    public void Configure(EntityTypeBuilder<BundleSlotOption> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PriceDelta).HasPrecision(19, 4);

        builder.HasIndex(x => x.BundleSlotId);
        builder.HasIndex(x => x.ProductVariantId);
    }
}
