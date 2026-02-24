using Aonik.Finance.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class CatalogBillerServiceConfiguration : IEntityTypeConfiguration<CatalogBillerService>
{
    public void Configure(EntityTypeBuilder<CatalogBillerService> builder)
    {
        builder.ToTable("CatalogBillerServices");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.BillerId)
            .IsRequired();

        builder.Property(x => x.ServiceCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.MinAmount)
            .HasPrecision(19, 4);

        builder.Property(x => x.MaxAmount)
            .HasPrecision(19, 4);

        builder.Property(x => x.FieldsJson)
            .IsRequired();

        builder.Property(x => x.ValidationJson)
            .HasMaxLength(4000);

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.BillerId, x.SortOrder });
        builder.HasIndex(x => new { x.TenantId, x.BillerId, x.Name });
        builder.HasIndex(x => new { x.TenantId, x.ServiceCode });
    }
}
