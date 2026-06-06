using Aonik.Agents.Entities;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Agents.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="TenantHttpTool"/> (Spec 033 §8.4 / §9). Final table name
/// is applied by the owning DbContext's prefix mapping (AnkTenantHttpTools). The declared
/// parameter schema and encrypted auth blob are stored as <c>nvarchar(max)</c>.
/// </summary>
internal class TenantHttpToolConfiguration : IEntityTypeConfiguration<TenantHttpTool>
{
    public void Configure(EntityTypeBuilder<TenantHttpTool> builder)
    {
        builder.ToTable("TenantHttpTools", SchemaNames.Default);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.Method).IsRequired().HasMaxLength(10);
        builder.Property(x => x.UrlTemplate).IsRequired().HasMaxLength(2048);
        builder.Property(x => x.ParameterSchemaJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ProtectedAuthJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ActionKind).HasMaxLength(400);
        builder.Property(x => x.ProposalType).HasMaxLength(200);
        builder.Property(x => x.ReviewNotes).HasMaxLength(2000);

        builder.Property(x => x.AuthKind)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(x => x.RiskTier)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.ApprovalState)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(x => new { x.TenantId, x.Name })
            .IsUnique()
            .HasDatabaseName("IX_TenantHttpTool_Tenant_Name");

        builder.HasIndex(x => new { x.TenantId, x.IsActive, x.ApprovalState })
            .HasDatabaseName("IX_TenantHttpTool_Tenant_Active_State");
    }
}
