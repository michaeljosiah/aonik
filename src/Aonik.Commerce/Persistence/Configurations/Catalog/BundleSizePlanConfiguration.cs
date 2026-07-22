using Aonik.Commerce.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Catalog;

public class BundleSizePlanConfiguration : IEntityTypeConfiguration<BundleSizePlan>
{
    public void Configure(EntityTypeBuilder<BundleSizePlan> builder)
    {
        builder.HasKey(x => x.Id);

        // decimal(19,4): these amounts combine with ProductPrice and the Spec 066 option amounts
        // in one quote, so they share that scale (Spec 068 §13).
        builder.Property(x => x.BasePrice).IsRequired().HasPrecision(19, 4);
        builder.Property(x => x.PerSpacePrice).IsRequired().HasPrecision(19, 4);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);

        builder.HasMany(x => x.Presets)
            .WithOne()
            .HasForeignKey(x => x.BundleSizePlanId)
            .OnDelete(DeleteBehavior.Cascade);

        // One plan per bundle product.
        builder.HasIndex(x => new { x.TenantId, x.BundleProductId }).IsUnique();
    }
}
