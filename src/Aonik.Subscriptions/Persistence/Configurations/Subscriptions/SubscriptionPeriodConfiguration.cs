using Aonik.Subscriptions.Entities.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Subscriptions.Persistence.Configurations.Subscriptions;

internal sealed class SubscriptionPeriodConfiguration : IEntityTypeConfiguration<SubscriptionPeriod>
{
    public void Configure(EntityTypeBuilder<SubscriptionPeriod> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);

        // The renewal anchor. A job that runs twice cannot mint a second period for the same
        // sequence, which is what stops it double-billing.
        builder.HasIndex(x => new { x.SubscriptionId, x.Sequence }).IsUnique();

        // The dunning selector.
        builder.HasIndex(x => new { x.TenantId, x.Status, x.NextAttemptAt });
    }
}
