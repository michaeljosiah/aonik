using Aonik.Commerce.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Catalog;

public class CollectionConfiguration : IEntityTypeConfiguration<Collection>
{
    public void Configure(EntityTypeBuilder<Collection> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Slug).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Subtitle).HasMaxLength(256);
        builder.Property(x => x.Kind).IsRequired().HasMaxLength(32);

        // Unfiltered on purpose: collections retire via IsActive and have no delete path, so a
        // soft-deleted row never needs to release the slug (unlike CollectionItem, whose
        // full-replace genuinely deletes rows).
        builder.HasIndex(x => new { x.TenantId, x.Slug }).IsUnique();

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.CollectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
