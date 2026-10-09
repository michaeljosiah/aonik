using Aonik.Commerce.Entities.Fulfilment;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Fulfilment;

public sealed class OrderGiftCardDeliveryConfiguration : IEntityTypeConfiguration<OrderGiftCardDelivery>
{
    public void Configure(EntityTypeBuilder<OrderGiftCardDelivery> builder)
    {
        builder.HasKey(row => row.Id);
        builder.Property(row => row.PurchaseSnapshotJson).IsRequired().HasMaxLength(10000);
        builder.Property(row => row.DeliveryMethod).IsRequired().HasMaxLength(16);
        builder.Property(row => row.Status).IsRequired().HasMaxLength(32);
        builder.Property(row => row.MaskedCode).IsRequired().HasMaxLength(64);
        builder.Property(row => row.PostingDate).HasColumnType("date");
        builder.HasIndex(row => new { row.TenantId, row.OrderId, row.OrderItemId }).IsUnique();
        builder.HasIndex(row => new { row.TenantId, row.PaymentIntentId }).IsUnique();
        builder.HasIndex(row => new { row.TenantId, row.DeliveryMethod, row.PostingDate, row.Status });
    }
}
