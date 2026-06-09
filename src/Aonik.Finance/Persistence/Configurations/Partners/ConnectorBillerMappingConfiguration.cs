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

        // Provider-code identity key for the idempotent import lookup (Spec 040 §8.4 / O1): makes the
        // "have I imported this provider biller/item before?" lookup an index seek and a hard
        // uniqueness guarantee. HasFilter(null) overrides EF's default "[ProviderItemCode] IS NOT NULL"
        // filter so the index ALSO covers biller-level rows (null ProviderItemCode) — SQL Server treats
        // NULLs as equal, enforcing exactly one biller-level mapping per (Tenant, Connector,
        // ProviderBillerCode), which is the import's idempotency anchor (§8).
        builder.HasIndex(x => new { x.TenantId, x.ConnectorId, x.ProviderBillerCode, x.ProviderItemCode })
            .IsUnique()
            .HasFilter(null);

        builder.HasOne<Connector>().WithMany().HasForeignKey(x => x.ConnectorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CatalogBiller>().WithMany().HasForeignKey(x => x.CatalogBillerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CatalogBillerService>().WithMany().HasForeignKey(x => x.CatalogBillerServiceId).OnDelete(DeleteBehavior.Restrict);
    }
}
