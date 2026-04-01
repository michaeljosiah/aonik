using Aonik.Ai.Entities;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Ai.Persistence.Configurations;

internal sealed class CustomerInsightAiSummaryConfiguration : IEntityTypeConfiguration<CustomerInsightAiSummary>
{
    public void Configure(EntityTypeBuilder<CustomerInsightAiSummary> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.NarrativeVersion)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.SummaryJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.FailureReason)
            .HasMaxLength(1000);

        builder.HasOne<AiRun>()
            .WithMany()
            .HasForeignKey(x => x.AiRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CustomerInsightAiSummary>()
            .WithMany()
            .HasForeignKey(x => x.SupersededById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Status })
            .HasFilter($"[Status] = '{CustomerInsightAiSummaryContract.StatusCurrent}'")
            .HasDatabaseName("IX_CustomerInsightAiSummaries_TenantUser_Current");

        builder.HasIndex(x => new { x.CustomerInsightSnapshotId, x.Status })
            .HasFilter($"[Status] = '{CustomerInsightAiSummaryContract.StatusCurrent}'")
            .HasDatabaseName("IX_CustomerInsightAiSummaries_Snapshot_Current");

        builder.HasIndex(x => x.AiRunId)
            .HasDatabaseName("IX_CustomerInsightAiSummaries_AiRunId");

        builder.HasIndex(x => x.SupersededById)
            .HasDatabaseName("IX_CustomerInsightAiSummaries_SupersededById");
    }
}
