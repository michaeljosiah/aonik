using Aonik.Subscriptions.Entities.Usage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Subscriptions.Persistence.Configurations.Usage;

internal sealed class CeilingHoldingConfiguration : IEntityTypeConfiguration<CeilingHolding>
{
    public void Configure(EntityTypeBuilder<CeilingHolding> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SubscriberKind).IsRequired().HasMaxLength(32);
        builder.Property(x => x.MeterCode).IsRequired().HasMaxLength(100);

        // One counter row per subscriber per ceiling. Without this two rows could each believe
        // they hold the whole allowance.
        builder.HasIndex(x => new { x.TenantId, x.SubscriberKind, x.SubscriberId, x.MeterCode })
            .IsUnique()
            .HasDatabaseName("IX_AnkCeilingHoldings_Subscriber_Unique");

        builder.HasMany(x => x.Claims)
            .WithOne()
            .HasForeignKey(x => x.CeilingHoldingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CeilingClaimConfiguration : IEntityTypeConfiguration<CeilingClaim>
{
    public void Configure(EntityTypeBuilder<CeilingClaim> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.HolderRef).IsRequired().HasMaxLength(200);

        // The idempotency key: one claim per holder. A retried create collides here rather than
        // silently consuming a second slot.
        builder.HasIndex(x => new { x.CeilingHoldingId, x.HolderRef })
            .IsUnique()
            .HasDatabaseName("IX_AnkCeilingClaims_Holder_Unique");
    }
}
