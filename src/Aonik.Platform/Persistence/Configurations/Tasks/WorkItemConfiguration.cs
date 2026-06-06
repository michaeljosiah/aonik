using Aonik.Platform.Entities.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations.Tasks;

/// <summary>
/// EF configuration for <see cref="WorkItem"/> (Spec 034). The bare table name
/// is overridden with the <c>Ank</c> prefix by the DbContext table mapping
/// (<c>MapPlatformTable</c>/<c>MapTable</c>), producing <c>dbo.AnkWorkItems</c>.
/// </summary>
public class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.ToTable("WorkItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();

        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Kind).IsRequired().HasMaxLength(40);

        builder.Property(x => x.SubjectType).HasMaxLength(100);

        builder.Property(x => x.AssigneeType).IsRequired().HasMaxLength(40);
        builder.Property(x => x.AssigneeKey).HasMaxLength(200);

        builder.Property(x => x.ActionType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ActionPayloadJson).IsRequired().HasColumnType("nvarchar(max)");

        builder.Property(x => x.ScheduleType).IsRequired().HasMaxLength(40);
        builder.Property(x => x.RecurrenceCron).HasMaxLength(120);
        builder.Property(x => x.Timezone).HasMaxLength(100);

        builder.Property(x => x.Status).IsRequired().HasMaxLength(40);
        builder.Property(x => x.SourceModule).IsRequired().HasMaxLength(100);
        builder.Property(x => x.CorrelationId).HasMaxLength(200);

        builder.Property(x => x.LeasedBy).HasMaxLength(200);
        builder.Property(x => x.LastError).HasMaxLength(4000);

        // Hot path: the dispatcher's due-scan filters by Status + NextRunAtUtc.
        builder.HasIndex(x => new { x.Status, x.NextRunAtUtc })
            .HasDatabaseName("IX_WorkItem_Status_NextRunAtUtc");

        builder.HasIndex(x => new { x.SubjectType, x.SubjectId })
            .HasDatabaseName("IX_WorkItem_Subject");

        builder.HasIndex(x => new { x.AssigneeType, x.AssigneeId })
            .HasDatabaseName("IX_WorkItem_Assignee");

        builder.HasIndex(x => x.CorrelationId)
            .HasDatabaseName("IX_WorkItem_CorrelationId");
    }
}
