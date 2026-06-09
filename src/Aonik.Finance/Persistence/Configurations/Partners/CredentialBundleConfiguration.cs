using Aonik.Finance.Entities.Partners;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.Partners;

internal class CredentialBundleConfiguration : IEntityTypeConfiguration<CredentialBundle>
{
    public void Configure(EntityTypeBuilder<CredentialBundle> builder)
    {
        builder.ToTable("CredentialBundles");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Ref).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ConnectorKind).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ProtectedSecretsJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.FieldMetadataJson).HasColumnType("nvarchar(max)");

        // The immutable Ref is the binding handle Connector.CredentialsRef stores; one per tenant.
        builder.HasIndex(x => new { x.TenantId, x.Ref })
            .IsUnique()
            .HasDatabaseName("UX_CredentialBundles_TenantId_Ref");
    }
}
