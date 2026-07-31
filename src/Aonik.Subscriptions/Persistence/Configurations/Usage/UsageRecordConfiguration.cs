using Aonik.Subscriptions.Entities.Usage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Subscriptions.Persistence.Configurations.Usage;

internal sealed class UsageRecordConfiguration : IEntityTypeConfiguration<UsageRecord>
{
    public void Configure(EntityTypeBuilder<UsageRecord> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SubscriberKind).IsRequired().HasMaxLength(32);
        builder.Property(x => x.MeterCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.SourceType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Quantity).HasPrecision(19, 4);
        builder.Property(x => x.ProviderCost).HasPrecision(19, 4);
        builder.Property(x => x.ProviderCostCurrency).HasMaxLength(3);
        builder.Property(x => x.AllocationsJson).HasColumnType("nvarchar(max)");

        builder.HasIndex(x => new { x.TenantId, x.SubscriberKind, x.SubscriberId, x.MeterCode, x.OccurredAt });
        builder.HasIndex(x => new { x.TenantId, x.SourceType, x.SourceId });
    }
}
