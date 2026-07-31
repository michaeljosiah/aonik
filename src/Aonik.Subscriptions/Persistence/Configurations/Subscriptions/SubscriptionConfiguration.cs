using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Subscriptions.Entities.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Subscriptions.Persistence.Configurations.Subscriptions;

internal sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SubscriberKind).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);

        builder.HasIndex(x => new { x.TenantId, x.SubscriberKind, x.SubscriberId });

        // The renewal selector: due, active, not pending cancellation.
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CurrentPeriodEnd });

        // One active subscription per subscriber, enforced in STORAGE. A service-level check loses
        // the race between two concurrent Subscribe calls and both rows are then renewed — and
        // charged. Filtered so a subscriber may hold any number of cancelled or expired
        // subscriptions; only the live slot is exclusive. The filter must stay in lockstep with
        // SubscriptionStatuses.OccupiesActiveSlot.
        builder.HasIndex(x => new { x.TenantId, x.SubscriberKind, x.SubscriberId })
            .IsUnique()
            .HasDatabaseName("IX_AnkSubscriptions_ActiveSubscriber_Unique")
            .HasFilter($"[Status] IN ('{SubscriptionStatuses.Trialing}', '{SubscriptionStatuses.Active}', '{SubscriptionStatuses.PastDue}')");

        builder.HasMany(x => x.Periods)
            .WithOne()
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
