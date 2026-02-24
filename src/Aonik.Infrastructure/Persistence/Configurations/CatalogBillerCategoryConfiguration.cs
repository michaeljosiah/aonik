using Aonik.Finance.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class CatalogBillerCategoryConfiguration : IEntityTypeConfiguration<CatalogBillerCategory>
{
    public void Configure(EntityTypeBuilder<CatalogBillerCategory> builder)
    {
        builder.ToTable("CatalogBillerCategories");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CountryCode)
            .IsRequired()
            .HasMaxLength(2);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.IconUrl)
            .HasMaxLength(500);

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.CountryCode, x.Name })
            .IsUnique();

        builder.HasIndex(x => new { x.TenantId, x.CountryCode, x.SortOrder });
    }
}
