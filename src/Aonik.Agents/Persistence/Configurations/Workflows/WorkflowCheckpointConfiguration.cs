using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Aonik.Agents.Entities.Workflows;
using Aonik.SharedKernel.Persistence;

namespace Aonik.Agents.Persistence.Configurations.Workflows;

internal sealed class WorkflowCheckpointConfiguration : IEntityTypeConfiguration<WorkflowCheckpoint>
{
    public void Configure(EntityTypeBuilder<WorkflowCheckpoint> builder)
    {
        builder.ToTable("WorkflowCheckpoints", SchemaNames.Default);
        builder.Property(checkpoint => checkpoint.FrameworkSessionId).HasMaxLength(128);
        builder.Property(checkpoint => checkpoint.CheckpointId).HasMaxLength(128).UseCollation("Latin1_General_100_BIN2");
        builder.Property(checkpoint => checkpoint.ParentCheckpointId).HasMaxLength(128);
        builder.Property(checkpoint => checkpoint.FrameworkVersion).HasMaxLength(40);
        builder.Property(checkpoint => checkpoint.StateJson).HasColumnType("nvarchar(max)");
        builder.HasIndex(checkpoint => new { checkpoint.TenantId, checkpoint.RunId, checkpoint.CheckpointId }).IsUnique();
        builder.HasIndex(checkpoint => new { checkpoint.TenantId, checkpoint.RunId, checkpoint.Sequence }).IsUnique();
    }
}
