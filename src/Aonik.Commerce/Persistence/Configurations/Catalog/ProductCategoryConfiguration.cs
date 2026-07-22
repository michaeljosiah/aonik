using Aonik.Commerce.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Catalog;

public class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Slug).IsRequired().HasMaxLength(160);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);

        // Spec 070 §11 — the retire-don't-delete lifecycle column. The database default matters:
        // a bool column added without one backfills existing rows FALSE, which would silently hide
        // every pre-existing category from the public tree the moment the migration ran.
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasIndex(x => new { x.TenantId, x.Slug }).IsUnique();
        builder.HasIndex(x => x.ParentCategoryId);
    }
}
