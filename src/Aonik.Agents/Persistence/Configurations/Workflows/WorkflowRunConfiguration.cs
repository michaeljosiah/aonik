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
        builder.Property(r => r.DefinitionHash).HasMaxLength(64);
        builder.Property(r => r.Mode).HasMaxLength(16).HasDefaultValue("Legacy");
        builder.Property(r => r.ActorType).HasMaxLength(24);
        builder.Property(r => r.SubjectType).HasMaxLength(40);
        builder.Property(r => r.IdempotencyKey).HasMaxLength(128).UseCollation("Latin1_General_100_BIN2");
        builder.Property(r => r.InputHash).HasMaxLength(64);
        builder.Property(r => r.FactsJson).HasColumnType("nvarchar(max)");
        builder.Property(r => r.FactsHash).HasMaxLength(64);
        builder.Property(r => r.Outcome).HasMaxLength(64);
        builder.Property(r => r.ErrorCode).HasMaxLength(80);
        builder.Property(r => r.CheckpointId).HasMaxLength(128);
        builder.Property(r => r.FrameworkSessionId).HasMaxLength(128);
        builder.HasIndex(r => new { r.TenantId, r.ActorType, r.ActorId, r.IdempotencyKey })
            .IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
        builder.HasIndex(r => new { r.TenantId, r.WorkflowVersionId, r.Status });

        // Detail page lists the most recent runs first — index supports
        // both filter-by-workflow and the descending start-time sort.
        builder.HasIndex(r => new { r.WorkflowId, r.StartedAt })
            .HasDatabaseName("IX_WorkflowRuns_WorkflowId_StartedAt");
    }
}
