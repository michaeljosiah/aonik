using Aonik.Finance.Entities.GiftCards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.GiftCards;

public sealed class GiftCardCheckoutAttemptConfiguration : IEntityTypeConfiguration<GiftCardCheckoutAttempt>
{
    public void Configure(EntityTypeBuilder<GiftCardCheckoutAttempt> builder)
    {
        builder.ToTable("AnkGiftCardCheckoutAttempts", "dbo");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(24).IsRequired();
        builder.Property(x => x.SnapshotJson).HasMaxLength(262144).IsRequired();
        builder.Property(x => x.ReservedAmount).HasPrecision(19, 4);
        builder.HasIndex(x => new { x.TenantId, x.PaymentIntentId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.GiftCardId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.OrderId });
    }
}
