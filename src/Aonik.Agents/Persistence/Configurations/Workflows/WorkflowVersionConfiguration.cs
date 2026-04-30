using Aonik.Agents.Entities.Workflows;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Agents.Persistence.Configurations.Workflows;

internal class WorkflowVersionConfiguration : IEntityTypeConfiguration<WorkflowVersion>
{
    public void Configure(EntityTypeBuilder<WorkflowVersion> builder)
    {
        builder.ToTable("WorkflowVersions", SchemaNames.Default);

        builder.Property(v => v.Tag).HasMaxLength(32).IsRequired();
        builder.Property(v => v.Message).HasMaxLength(1000);
        builder.Property(v => v.AuthorName).HasMaxLength(200);
        builder.Property(v => v.AuthorColor).HasMaxLength(16);

        // Tag is unique per workflow — version bumps must be deterministic.
        builder.HasIndex(v => new { v.WorkflowId, v.Tag })
            .IsUnique()
            .HasDatabaseName("IX_WorkflowVersions_WorkflowId_Tag");
    }
}
