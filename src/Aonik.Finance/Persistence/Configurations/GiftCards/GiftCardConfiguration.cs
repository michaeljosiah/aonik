using Aonik.Finance.Entities.GiftCards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.GiftCards;

public sealed class GiftCardConfiguration : IEntityTypeConfiguration<GiftCard>
{
    public void Configure(EntityTypeBuilder<GiftCard> builder)
    {
        builder.ToTable("AnkGiftCards", "dbo");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.FaceValue).HasPrecision(19, 4);
        builder.Property(x => x.PolicySnapshotJson).HasMaxLength(32768).IsRequired();
        builder.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ProtectedCode).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.MaskedCode).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(24).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.CodeHash }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.CartId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.OrderItemId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.PaymentIntentId }).IsUnique();
    }
}
