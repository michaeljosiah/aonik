using Aonik.Commerce.Entities.Fulfilment;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Fulfilment;

public sealed class CartDeliveryReservationConfiguration : IEntityTypeConfiguration<CartDeliveryReservation>
{
    public void Configure(EntityTypeBuilder<CartDeliveryReservation> builder)
    {
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Status).IsRequired().HasMaxLength(32);
        builder.HasIndex(row => new { row.TenantId, row.CartId }).IsUnique();
        builder.HasIndex(row => new { row.TenantId, row.CapacityId, row.Status, row.ExpiresAtUtc });
        builder.HasIndex(row => new { row.TenantId, row.Status, row.PaymentDeadlineUtc });
        builder.HasIndex(row => new { row.TenantId, row.OrderId });
    }
}
