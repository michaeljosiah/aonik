using Aonik.Platform.Entities.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations.Tasks;

/// <summary>
/// EF configuration for <see cref="WorkItemRun"/> (Spec 034). The unique
/// <c>(WorkItemId, ScheduledForUtc)</c> index is the occurrence idempotency
/// anchor: the dispatcher cannot create two runs for the same occurrence even if
/// two workers race or the heartbeat double-fires. Maps to <c>dbo.AnkWorkItemRuns</c>.
/// </summary>
public class WorkItemRunConfiguration : IEntityTypeConfiguration<WorkItemRun>
{
    public void Configure(EntityTypeBuilder<WorkItemRun> builder)
    {
        builder.ToTable("WorkItemRuns");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.WorkItemId).IsRequired();
        builder.Property(x => x.ScheduledForUtc).IsRequired();
        builder.Property(x => x.StartedAtUtc).IsRequired();

        builder.Property(x => x.Outcome).IsRequired().HasMaxLength(40);
        builder.Property(x => x.ResultJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Error).HasMaxLength(4000);

        // One run per occurrence — the hard exactly-once backstop under concurrent dispatch.
        builder.HasIndex(x => new { x.WorkItemId, x.ScheduledForUtc })
            .IsUnique()
            .HasDatabaseName("IX_WorkItemRun_WorkItemId_ScheduledForUtc");
    }
}
