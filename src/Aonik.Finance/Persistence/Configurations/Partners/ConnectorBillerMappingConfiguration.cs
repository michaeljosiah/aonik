using Aonik.Finance.Entities.Catalog;
using Aonik.Finance.Entities.Partners;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.Partners;

internal class ConnectorBillerMappingConfiguration : IEntityTypeConfiguration<ConnectorBillerMapping>
{
    public void Configure(EntityTypeBuilder<ConnectorBillerMapping> builder)
    {
        builder.ToTable("ConnectorBillerMappings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProviderBillerCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ProviderItemCode).HasMaxLength(100);

        builder.HasIndex(x => new { x.TenantId, x.ConnectorId, x.CatalogBillerId, x.CatalogBillerServiceId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.CatalogBillerId });

        builder.HasOne<Connector>().WithMany().HasForeignKey(x => x.ConnectorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CatalogBiller>().WithMany().HasForeignKey(x => x.CatalogBillerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CatalogBillerService>().WithMany().HasForeignKey(x => x.CatalogBillerServiceId).OnDelete(DeleteBehavior.Restrict);
    }
}
