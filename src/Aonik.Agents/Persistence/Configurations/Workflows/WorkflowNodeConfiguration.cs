using Aonik.Agents.Entities.Workflows;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Agents.Persistence.Configurations.Workflows;

internal class WorkflowNodeConfiguration : IEntityTypeConfiguration<WorkflowNode>
{
    public void Configure(EntityTypeBuilder<WorkflowNode> builder)
    {
        builder.ToTable("WorkflowNodes", SchemaNames.Default);

        builder.Property(n => n.Kind).HasMaxLength(32).IsRequired();
        builder.Property(n => n.Label).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Summary).HasMaxLength(500);
        builder.Property(n => n.Notes).HasMaxLength(2000);
        builder.Property(n => n.ParamsJson).HasColumnType("nvarchar(max)");

        builder.HasIndex(n => n.WorkflowId)
            .HasDatabaseName("IX_WorkflowNodes_WorkflowId");
    }
}
