using Aonik.Commerce.Entities.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Promotions;

public class DiscountConfiguration : IEntityTypeConfiguration<Discount>
{
    public void Configure(EntityTypeBuilder<Discount> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Kind).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Value).IsRequired().HasPrecision(19, 4);
        builder.Property(x => x.Currency).HasMaxLength(3);

        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}
