using Aonik.Finance.Entities.Catalog;
using Aonik.Finance.Entities.Partners;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class CatalogBillerConfiguration : IEntityTypeConfiguration<CatalogBiller>
{
    public void Configure(EntityTypeBuilder<CatalogBiller> builder)
    {
        builder.ToTable("CatalogBillers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CategoryId)
            .IsRequired();

        builder.Property(x => x.CorrespondentPartnerId)
            .IsRequired();

        builder.Property(x => x.CountryCode)
            .IsRequired()
            .HasMaxLength(2);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.LogoUrl)
            .HasMaxLength(500);

        builder.Property(x => x.BannerUrl)
            .HasMaxLength(500);

        builder.Property(x => x.SupportPhone)
            .HasMaxLength(50);

        builder.Property(x => x.SupportEmail)
            .HasMaxLength(200);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.IsFeatured)
            .IsRequired();

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.CountryCode, x.CategoryId, x.SortOrder });
        builder.HasIndex(x => new { x.TenantId, x.CountryCode, x.Name });
        builder.HasIndex(x => new { x.TenantId, x.CorrespondentPartnerId });

        builder.HasOne<Partner>()
            .WithMany()
            .HasForeignKey(x => x.CorrespondentPartnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
