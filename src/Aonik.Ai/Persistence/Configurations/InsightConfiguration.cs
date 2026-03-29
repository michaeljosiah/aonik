using Aonik.Ai.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Ai.Persistence.Configurations;

internal class InsightConfiguration : IEntityTypeConfiguration<Insight>
{
    public void Configure(EntityTypeBuilder<Insight> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.SubjectType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.SubjectId)
            .IsRequired();

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Summary)
            .IsRequired();

        builder.Property(x => x.MetadataJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.CreatedUtc)
            .IsRequired();

        // Tenant-scoped subject lookup (replaces old non-tenant index)
        builder.HasIndex(x => new { x.TenantId, x.SubjectType, x.SubjectId })
            .HasDatabaseName("IX_Insights_Tenant_SubjectType_SubjectId");

        // User-specific insight lookup (behavioural insights)
        builder.HasIndex(x => new { x.TenantId, x.UserId })
            .HasFilter("[UserId] IS NOT NULL")
            .HasDatabaseName("IX_Insights_Tenant_UserId");

        builder.HasIndex(x => x.CreatedUtc);
    }
}
