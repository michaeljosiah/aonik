using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Aonik.Agents.Entities.Workflows;
using Aonik.SharedKernel.Persistence;

namespace Aonik.Agents.Persistence.Configurations.Workflows;

internal sealed class WorkflowStepRunConfiguration : IEntityTypeConfiguration<WorkflowStepRun>
{
    public void Configure(EntityTypeBuilder<WorkflowStepRun> builder)
    {
        builder.ToTable("WorkflowStepRuns", SchemaNames.Default);
        builder.Property(step => step.NodeKey).HasMaxLength(128).UseCollation("Latin1_General_100_BIN2");
        builder.Property(step => step.InvocationPath).HasMaxLength(200).UseCollation("Latin1_General_100_BIN2");
        builder.Property(step => step.Status).HasMaxLength(32);
        builder.Property(step => step.ActionKey).HasMaxLength(160);
        builder.Property(step => step.RequiredPermission).HasMaxLength(160);
        builder.Property(step => step.ArgumentsJson).HasColumnType("nvarchar(max)");
        builder.Property(step => step.ArgumentsHash).HasMaxLength(64);
        builder.Property(step => step.FactsHash).HasMaxLength(64);
        builder.Property(step => step.FactsJson).HasColumnType("nvarchar(max)");
        builder.Property(step => step.ExpectedSubjectVersion).HasMaxLength(128);
        builder.Property(step => step.WaitCheckpointId).HasMaxLength(128);
        builder.Property(step => step.ResultJson).HasColumnType("nvarchar(max)");
        builder.Property(step => step.DecisionTraceJson).HasColumnType("nvarchar(max)");
        builder.Property(step => step.SelectedRoute).HasMaxLength(100);
        builder.Property(step => step.ErrorCode).HasMaxLength(80);
        builder.HasIndex(step => new { step.TenantId, step.RunId, step.NodeKey, step.InvocationPath }).IsUnique();
        builder.HasIndex(step => new { step.TenantId, step.OperationId }).IsUnique();
    }
}
