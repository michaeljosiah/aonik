using Aonik.Finance.Entities.Partners;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.Partners;

/// <summary>
/// Connector instance mapping. Historically the entity had no configuration (convention-only); this class
/// adds the legacy-default marker introduced by Spec 042 §7.2 without altering the existing columns, so the
/// generated migration stays minimal.
/// </summary>
internal class ConnectorConfiguration : IEntityTypeConfiguration<Connector>
{
    public void Configure(EntityTypeBuilder<Connector> builder)
    {
        builder.ToTable("Connectors");
        builder.HasKey(x => x.Id);

        // ConnectorType holds a short kind code (e.g. "flutterwave-payout-v4"). It must be bounded so it can
        // participate in the legacy-default unique index below — SQL Server cannot index an nvarchar(max)
        // column. 100 chars is ample for any registry kind code.
        builder.Property(x => x.ConnectorType).IsRequired().HasMaxLength(100);

        builder.Property(x => x.IsLegacyDefault).HasDefaultValue(false);

        // At most one legacy-default connector per kind per tenant (Spec 042 §7.2): a FILTERED unique index
        // so any number of non-default connectors of the same kind remain unconstrained. Only the default
        // connector falls back to the legacy Finance.Partners.Flutterwave.* keys.
        builder.HasIndex(x => new { x.TenantId, x.ConnectorType })
            .IsUnique()
            .HasFilter("[IsLegacyDefault] = 1")
            .HasDatabaseName("UX_Connectors_TenantId_ConnectorType_LegacyDefault");
    }
}
