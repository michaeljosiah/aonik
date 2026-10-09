using Aonik.Finance.Entities.Loyalty;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.Loyalty;

public sealed class LoyaltyCheckoutAttemptConfiguration : IEntityTypeConfiguration<LoyaltyCheckoutAttempt>
{
    public void Configure(EntityTypeBuilder<LoyaltyCheckoutAttempt> builder)
    {
        builder.ToTable("AnkLoyaltyCheckoutAttempts", "dbo");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(24).IsRequired();
        builder.Property(x => x.SnapshotJson).HasMaxLength(262144).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.PaymentIntentId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.AccountId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.OrderId });
    }
}
