using Aonik.Commerce.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Catalog;

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Sku).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.OptionsJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.WeightGrams).HasPrecision(19, 4);

        builder.HasMany(x => x.Prices)
            .WithOne()
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => new { x.TenantId, x.Sku }).IsUnique();
    }
}
