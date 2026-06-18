using Aonik.Commerce.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Catalog;

public class BundleSlotConfiguration : IEntityTypeConfiguration<BundleSlot>
{
    public void Configure(EntityTypeBuilder<BundleSlot> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(128);

        builder.HasMany(x => x.Options)
            .WithOne()
            .HasForeignKey(x => x.BundleSlotId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.BundleProductId);
    }
}
