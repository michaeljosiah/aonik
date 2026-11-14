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
        builder.Property(n => n.LogicalKey).HasMaxLength(128).UseCollation("Latin1_General_100_BIN2");
        builder.HasIndex(n => new { n.TenantId, n.WorkflowId, n.LogicalKey })
            .IsUnique().HasFilter("[LogicalKey] IS NOT NULL AND [IsDeleted] = 0");

        builder.HasIndex(n => n.WorkflowId)
            .HasDatabaseName("IX_WorkflowNodes_WorkflowId");
    }
}
