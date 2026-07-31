using Aonik.Subscriptions.Entities.Usage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Subscriptions.Persistence.Configurations.Usage;

internal sealed class UsageReservationAllocationConfiguration : IEntityTypeConfiguration<UsageReservationAllocation>
{
    public void Configure(EntityTypeBuilder<UsageReservationAllocation> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity).HasPrecision(19, 4);

        // Release and sweep resolve holds from either side.
        builder.HasIndex(x => x.ReservationId);
        builder.HasIndex(x => x.GrantId);
    }
}
