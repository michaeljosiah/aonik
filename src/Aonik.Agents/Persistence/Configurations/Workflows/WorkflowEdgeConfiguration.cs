using Aonik.Agents.Entities.Workflows;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Agents.Persistence.Configurations.Workflows;

internal class WorkflowEdgeConfiguration : IEntityTypeConfiguration<WorkflowEdge>
{
    public void Configure(EntityTypeBuilder<WorkflowEdge> builder)
    {
        builder.ToTable("WorkflowEdges", SchemaNames.Default);

        builder.Property(e => e.Label).HasMaxLength(64);

        builder.HasIndex(e => e.WorkflowId)
            .HasDatabaseName("IX_WorkflowEdges_WorkflowId");
    }
}
