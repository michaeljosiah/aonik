using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Aonik.Platform.Entities.Integrations;

namespace Aonik.Platform.Persistence.Configurations.Integrations;

internal sealed class ConnectorConnectionGrantConfiguration : IEntityTypeConfiguration<ConnectorConnectionGrant>
{
    public void Configure(EntityTypeBuilder<ConnectorConnectionGrant> builder)
    {
        builder.Property(grant => grant.GranteeType).HasMaxLength(24);
        builder.Property(grant => grant.AllowedActionsJson).HasColumnType("nvarchar(max)");
        builder.HasIndex(grant => new { grant.TenantId, grant.ConnectionId, grant.GranteeType, grant.GranteeId })
            .IsUnique().HasFilter("[RevokedAt] IS NULL");
        builder.HasIndex(grant => new { grant.TenantId, grant.GranteeType, grant.GranteeId, grant.RevokedAt });
    }
}
