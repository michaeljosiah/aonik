using Aonik.Commerce.Entities.Fulfilment;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Fulfilment;

public sealed class DeliveryDateCapacityConfiguration : IEntityTypeConfiguration<DeliveryDateCapacity>
{
    public void Configure(EntityTypeBuilder<DeliveryDateCapacity> builder)
    {
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Unit).IsRequired().HasMaxLength(16);
        builder.HasIndex(row => new { row.TenantId, row.DeliveryDate }).IsUnique();
        builder.ToTable("DeliveryDateCapacities", table => table.HasCheckConstraint(
            "CK_DeliveryDateCapacity_Nonnegative", "[Capacity] >= 0"));
    }
}
