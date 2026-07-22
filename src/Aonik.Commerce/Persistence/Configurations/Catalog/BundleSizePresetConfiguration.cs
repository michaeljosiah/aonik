using Aonik.Commerce.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Catalog;

public class BundleSizePresetConfiguration : IEntityTypeConfiguration<BundleSizePreset>
{
    public void Configure(EntityTypeBuilder<BundleSizePreset> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Price).IsRequired().HasPrecision(19, 4);
        builder.Property(x => x.SavingAmount).HasPrecision(19, 4);
        builder.Property(x => x.Badge).HasMaxLength(64);
        builder.Property(x => x.Blurb).HasMaxLength(256);

        // One LIVE price point per size within a plan — filtered so soft-deleted presets from a
        // full replace never block re-adding the same size.
        builder.HasIndex(x => new { x.TenantId, x.BundleSizePlanId, x.Size })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
