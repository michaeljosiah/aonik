using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Aonik.Platform.Entities.Integrations;

namespace Aonik.Platform.Persistence.Configurations.Integrations;

internal sealed class ConnectorConnectionRequestConfiguration : IEntityTypeConfiguration<ConnectorConnectionRequest>
{
    public void Configure(EntityTypeBuilder<ConnectorConnectionRequest> builder)
    {
        builder.Property(request => request.ConnectorKey).HasMaxLength(80);
        builder.Property(request => request.DesiredCapability).HasMaxLength(500);
        builder.Property(request => request.Ownership).HasMaxLength(16);
        builder.Property(request => request.Purpose).HasMaxLength(1000);
        builder.Property(request => request.RequestHash).HasMaxLength(64);
        builder.Property(request => request.ActiveRequestHash).HasMaxLength(64);
        builder.Property(request => request.InputHash).HasMaxLength(64);
        builder.Property(request => request.IdempotencyKey).HasMaxLength(128).UseCollation("Latin1_General_100_BIN2");
        builder.Property(request => request.Status).HasMaxLength(24);
        builder.Property(request => request.ReasonCode).HasMaxLength(80);
        builder.Property(request => request.NextAction).HasMaxLength(500);
        builder.Property(request => request.OAuthStateHash).HasMaxLength(64);
        builder.Property(request => request.ConsentedAccountId).HasMaxLength(200);
        builder.Property(request => request.ConsentedAccountLabel).HasMaxLength(254);
        builder.Property(request => request.ActionsJson).HasColumnType("nvarchar(max)");
        builder.Property(request => request.RequiredScopesJson).HasColumnType("nvarchar(max)");
        builder.Property(request => request.ProtectedVerifier).HasColumnType("nvarchar(max)");
        builder.Property(request => request.ProtectedOAuthResult).HasColumnType("nvarchar(max)");
        builder.HasIndex(request => new { request.TenantId, request.RequesterUserId, request.IdempotencyKey }).IsUnique();
        builder.HasIndex(request => new { request.TenantId, request.ActiveRequestHash }).IsUnique().HasFilter("[ActiveRequestHash] IS NOT NULL");
        builder.HasIndex(request => new { request.TenantId, request.RequesterUserId, request.Status });
        builder.HasIndex(request => new { request.TenantId, request.ReviewerRoleId, request.Status });
    }
}
