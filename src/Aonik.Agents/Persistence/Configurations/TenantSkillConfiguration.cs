using Aonik.Agents.Entities;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Agents.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="TenantSkill"/> (Spec 033 §8.1 / §9). Final table name is
/// applied by the owning DbContext's prefix mapping (AnkTenantSkills); this configures columns,
/// the string-valued approval state, and the per-tenant uniqueness of the skill name.
/// </summary>
internal class TenantSkillConfiguration : IEntityTypeConfiguration<TenantSkill>
{
    public void Configure(EntityTypeBuilder<TenantSkill> builder)
    {
        builder.ToTable("TenantSkills", SchemaNames.Default);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Version).HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.StorageKey).HasMaxLength(1024);
        builder.Property(x => x.Sha256).HasMaxLength(64);
        builder.Property(x => x.FrontmatterJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.AllowedToolsJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ReviewNotes).HasMaxLength(2000);

        builder.Property(x => x.ApprovalState)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        // One skill name per tenant.
        builder.HasIndex(x => new { x.TenantId, x.Name })
            .IsUnique()
            .HasDatabaseName("IX_TenantSkill_Tenant_Name");

        // Hot path: the skills source reads a tenant's active skills at agent build.
        builder.HasIndex(x => new { x.TenantId, x.IsActive, x.ApprovalState })
            .HasDatabaseName("IX_TenantSkill_Tenant_Active_State");
    }
}
