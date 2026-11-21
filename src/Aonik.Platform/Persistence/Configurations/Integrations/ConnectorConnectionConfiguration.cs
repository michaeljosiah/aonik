using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Aonik.Platform.Entities.Integrations;

namespace Aonik.Platform.Persistence.Configurations.Integrations;

internal sealed class ConnectorConnectionConfiguration : IEntityTypeConfiguration<ConnectorConnection>
{
    public void Configure(EntityTypeBuilder<ConnectorConnection> builder)
    {
        builder.Property(connection => connection.ConnectorKey).HasMaxLength(80);
        builder.Property(connection => connection.Ownership).HasMaxLength(16);
        builder.Property(connection => connection.DisplayName).HasMaxLength(160);
        builder.Property(connection => connection.ProviderAccountId).HasMaxLength(200).UseCollation("Latin1_General_100_BIN2");
        builder.Property(connection => connection.ProviderAccountLabel).HasMaxLength(254);
        builder.Property(connection => connection.Status).HasMaxLength(32);
        builder.Property(connection => connection.LastErrorCode).HasMaxLength(80);
        builder.Property(connection => connection.GrantedScopesJson).HasColumnType("nvarchar(max)");
        builder.Property(connection => connection.ProtectedCredentials).HasColumnType("nvarchar(max)");
        builder.HasIndex(connection => new { connection.TenantId, connection.ConnectorKey, connection.Ownership, connection.OwnerUserId });
        builder.HasIndex(connection => new { connection.TenantId, connection.ConnectorKey, connection.Ownership, connection.OwnerUserId, connection.ProviderAccountId })
            .IsUnique().HasFilter("[ProviderAccountId] <> N'' AND [Status] <> N'Revoked'");
    }
}
