using Aonik.Platform.Entities.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations.Operations;

internal sealed class AzureMonitorAlertEventConfiguration : IEntityTypeConfiguration<AzureMonitorAlertEvent>
{
    public void Configure(EntityTypeBuilder<AzureMonitorAlertEvent> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.Provider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ExternalAlertId)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.AlertRuleName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.AlertRuleId)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.MonitorCondition)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(x => x.Severity)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.SignalType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.MonitoringService)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.NormalizedType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.CorrelationKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(x => x.ResourceIdsJson)
            .IsRequired();

        builder.Property(x => x.EssentialsJson)
            .IsRequired();

        builder.Property(x => x.AlertContextJson)
            .IsRequired();

        builder.Property(x => x.CustomPropertiesJson)
            .IsRequired();

        builder.Property(x => x.AnalysisSummary)
            .IsRequired();

        builder.Property(x => x.AnalysisJson)
            .IsRequired();

        builder.Property(x => x.LastError)
            .HasMaxLength(1000);

        builder.HasIndex(x => x.ExternalAlertId)
            .HasDatabaseName("IX_AzureMonitorAlertEvent_ExternalAlertId")
            .IsUnique();

        builder.HasIndex(x => new { x.CorrelationKey, x.ReceivedAtUtc })
            .HasDatabaseName("IX_AzureMonitorAlertEvent_CorrelationKey_ReceivedAtUtc");

        builder.HasIndex(x => new { x.Status, x.ReceivedAtUtc })
            .HasDatabaseName("IX_AzureMonitorAlertEvent_Status_ReceivedAtUtc");
    }
}
