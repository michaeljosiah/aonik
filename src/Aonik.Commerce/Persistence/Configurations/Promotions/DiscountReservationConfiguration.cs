using Aonik.Commerce.Entities.Promotions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Promotions;

public class DiscountReservationConfiguration : IEntityTypeConfiguration<DiscountReservation>
{
    public void Configure(EntityTypeBuilder<DiscountReservation> builder)
    {
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Status).IsRequired().HasMaxLength(16);
        builder.HasIndex(row => new { row.TenantId, row.CartId }).IsUnique();
        builder.HasIndex(row => new { row.TenantId, row.DiscountId, row.Status });
    }
}
