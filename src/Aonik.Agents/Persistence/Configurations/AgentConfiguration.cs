using Aonik.Agents.Entities;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Agents.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the Agent entity.
/// Maps to the dbo schema (created by existing migrations).
/// </summary>
internal class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.ToTable("Agents", SchemaNames.Default);

        builder.Property(a => a.Name).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Domain).HasMaxLength(100);
        builder.Property(a => a.Description).HasMaxLength(2000);
        builder.Property(a => a.InstructionsText).HasColumnType("nvarchar(max)");
        builder.Property(a => a.RiskTier).HasMaxLength(50);
        builder.Property(a => a.IconUrl).HasMaxLength(500);

        // Unique: one config per agent name per tenant (null = global)
        builder.HasIndex(a => new { a.TenantId, a.Name })
            .IsUnique()
            .HasDatabaseName("IX_Agents_TenantId_Name");
    }
}
