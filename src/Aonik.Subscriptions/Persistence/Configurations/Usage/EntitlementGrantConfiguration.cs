using Aonik.Subscriptions.Entities.Usage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Subscriptions.Persistence.Configurations.Usage;

internal sealed class EntitlementGrantConfiguration : IEntityTypeConfiguration<EntitlementGrant>
{
    public void Configure(EntityTypeBuilder<EntitlementGrant> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SubscriberKind).IsRequired().HasMaxLength(32);
        builder.Property(x => x.MeterCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Source).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Allowance).HasPrecision(19, 4);
        builder.Property(x => x.Consumed).HasPrecision(19, 4);
        builder.Property(x => x.Held).HasPrecision(19, 4);
        builder.Property(x => x.UnitValue).HasPrecision(19, 4);
        builder.Property(x => x.UnitValueCurrency).HasMaxLength(3);

        // The draw-down query, run on every reserve. Keyed by SUBSCRIBER so purchased grants
        // survive a cancel-and-resubscribe.
        builder.HasIndex(x => new { x.TenantId, x.SubscriberKind, x.SubscriberId, x.MeterCode, x.Status, x.ExpiresAt })
            .HasDatabaseName("IX_AnkEntitlementGrants_DrawDown");

        // One plan grant set per period. Payment completion is at-least-once, so a retried or
        // concurrently-handled event would otherwise silently double a subscriber's allowance —
        // a status check alone cannot carry that.
        builder.HasIndex(x => new { x.PeriodId, x.MeterCode, x.Source })
            .IsUnique()
            .HasDatabaseName("IX_AnkEntitlementGrants_PeriodMeterSource_Unique")
            .HasFilter("[PeriodId] IS NOT NULL");

        // One purchased grant per order line.
        builder.HasIndex(x => new { x.SourceOrderId, x.MeterCode })
            .IsUnique()
            .HasDatabaseName("IX_AnkEntitlementGrants_SourceOrderMeter_Unique")
            .HasFilter("[SourceOrderId] IS NOT NULL");
    }
}
