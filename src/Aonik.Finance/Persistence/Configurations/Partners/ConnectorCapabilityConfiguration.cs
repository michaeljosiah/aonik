using Aonik.Finance.Entities.Partners;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.Partners;

internal class ConnectorCapabilityConfiguration : IEntityTypeConfiguration<ConnectorCapability>
{
    public void Configure(EntityTypeBuilder<ConnectorCapability> builder)
    {
        builder.ToTable("ConnectorCapabilities");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Category).IsRequired().HasMaxLength(30);
        builder.Property(x => x.CountryCode).IsRequired().HasMaxLength(2);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Method).HasMaxLength(50);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(50);

        builder.HasIndex(x => new { x.TenantId, x.ConnectorId, x.Category });
        builder.HasIndex(x => new { x.TenantId, x.Category, x.CountryCode, x.Currency });

        builder.HasOne<Connector>().WithMany().HasForeignKey(x => x.ConnectorId).OnDelete(DeleteBehavior.Restrict);
    }
}
