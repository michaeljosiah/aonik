using Aonik.Commerce.Entities.Fulfilment;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Fulfilment;

public class OrderDeliveryDetailsConfiguration : IEntityTypeConfiguration<OrderDeliveryDetails>
{
    public void Configure(EntityTypeBuilder<OrderDeliveryDetails> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PurchaserEmail).IsRequired().HasMaxLength(254);
        builder.Property(x => x.PurchaserFirstName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.PurchaserLastName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.PurchaserPhone).IsRequired().HasMaxLength(32);
        builder.Property(x => x.AddressLine1).IsRequired().HasMaxLength(200);
        builder.Property(x => x.AddressLine2).HasMaxLength(200);
        builder.Property(x => x.City).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Region).HasMaxLength(100);
        builder.Property(x => x.Postcode).IsRequired().HasMaxLength(32);
        builder.Property(x => x.CountryCode).IsRequired().HasMaxLength(2);
        builder.Property(x => x.DeliveryDate).HasColumnType("date");
        builder.Property(x => x.Timezone).IsRequired().HasMaxLength(64);
        builder.Property(x => x.RecipientName).IsRequired().HasMaxLength(201);
        builder.Property(x => x.RecipientPhone).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.GreetingCardMessage).HasMaxLength(1000);
        // A deleted historical snapshot must never allow checkout to rewrite the same order.
        builder.HasIndex(x => new { x.TenantId, x.OrderId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.DeliveryDate, x.OrderId });
    }
}
