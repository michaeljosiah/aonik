using Aonik.Finance.Entities.Catalog;
using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Entities.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.Payments;

internal class BillValidationConfiguration : IEntityTypeConfiguration<BillValidation>
{
    public void Configure(EntityTypeBuilder<BillValidation> builder)
    {
        builder.ToTable("BillValidations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ClientReference).IsRequired().HasMaxLength(200);
        builder.Property(x => x.CustomerId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ValidationToken).IsRequired().HasMaxLength(200);
        builder.Property(x => x.CustomerName).HasMaxLength(256);
        builder.Property(x => x.OutstandingAmount).HasPrecision(19, 4);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.ResolvedFieldsJson).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(50);

        builder.HasIndex(x => new { x.TenantId, x.ValidationToken }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ClientReference });

        builder.HasOne<Connector>().WithMany().HasForeignKey(x => x.ConnectorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CatalogBillerService>().WithMany().HasForeignKey(x => x.CatalogBillerServiceId).OnDelete(DeleteBehavior.Restrict);
    }
}
