using Aonik.Agents.Entities;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Agents.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="TenantMcpServer"/> (Spec 033 §8.3 / §9). Final table name
/// is applied by the owning DbContext's prefix mapping (AnkTenantMcpServers). The encrypted auth
/// blob is stored as <c>nvarchar(max)</c>; the enums are persisted as strings.
/// </summary>
internal class TenantMcpServerConfiguration : IEntityTypeConfiguration<TenantMcpServer>
{
    public void Configure(EntityTypeBuilder<TenantMcpServer> builder)
    {
        builder.ToTable("TenantMcpServers", SchemaNames.Default);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Endpoint).IsRequired().HasMaxLength(2048);
        builder.Property(x => x.ProtectedAuthJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.AllowedToolPrefixesJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ReviewNotes).HasMaxLength(2000);

        builder.Property(x => x.TransportType)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.AuthKind)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(x => x.DefaultRiskTier)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.ApprovalState)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(x => new { x.TenantId, x.Name })
            .IsUnique()
            .HasDatabaseName("IX_TenantMcpServer_Tenant_Name");

        builder.HasIndex(x => new { x.TenantId, x.IsActive, x.ApprovalState })
            .HasDatabaseName("IX_TenantMcpServer_Tenant_Active_State");
    }
}
