using Aonik.Agents.Entities.Workflows;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Agents.Persistence.Configurations.Workflows;

internal class WorkflowRunConfiguration : IEntityTypeConfiguration<WorkflowRun>
{
    public void Configure(EntityTypeBuilder<WorkflowRun> builder)
    {
        builder.ToTable("WorkflowRuns", SchemaNames.Default);

        builder.Property(r => r.Status).HasMaxLength(32).IsRequired();
        builder.Property(r => r.StartedBy).HasMaxLength(500);
        builder.Property(r => r.SequenceJson).HasColumnType("nvarchar(max)");

        // Detail page lists the most recent runs first — index supports
        // both filter-by-workflow and the descending start-time sort.
        builder.HasIndex(r => new { r.WorkflowId, r.StartedAt })
            .HasDatabaseName("IX_WorkflowRuns_WorkflowId_StartedAt");
    }
}
