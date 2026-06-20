using Aonik.Ai.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Ai.Persistence.Configurations;

internal sealed class DecisionPatternConfiguration : IEntityTypeConfiguration<DecisionPattern>
{
    public void Configure(EntityTypeBuilder<DecisionPattern> builder)
    {
        // Table name (AnkDecisionPatterns) is applied by the DbContexts' MapTable/MapAiTable helpers,
        // matching the convention used for UserMemoryEntry — do not set ToTable here.
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.DecisionType)
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(x => x.Segment)
            .HasMaxLength(120);

        builder.Property(x => x.Statement)
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(x => x.PayloadJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Confidence)
            .IsRequired()
            .HasPrecision(3, 2);

        // Current patterns for a decision type / optional segment (SupersededAtUtc IS NULL is the hot read).
        builder.HasIndex(x => new { x.TenantId, x.DecisionType, x.Segment, x.SupersededAtUtc })
            .HasDatabaseName("IX_DecisionPatterns_Tenant_Type_Segment_Current");

        // Confidence ranking within a decision type.
        builder.HasIndex(x => new { x.TenantId, x.DecisionType, x.Confidence })
            .HasDatabaseName("IX_DecisionPatterns_Tenant_Type_Confidence");
    }
}
