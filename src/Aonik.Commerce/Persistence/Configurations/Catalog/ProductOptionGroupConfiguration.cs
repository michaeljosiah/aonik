using Aonik.Commerce.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Catalog;

public class ProductOptionGroupConfiguration : IEntityTypeConfiguration<ProductOptionGroup>
{
    public void Configure(EntityTypeBuilder<ProductOptionGroup> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AllowedChoiceKeysJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.DefaultChoiceKey).HasMaxLength(64);
        builder.Property(x => x.SelectionModeOverride).HasMaxLength(16);

        builder.HasIndex(x => new { x.TenantId, x.ProductId });

        builder.HasIndex(x => new { x.TenantId, x.ProductId, x.OptionGroupId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
