using Aonik.Commerce.Entities.Fulfilment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Fulfilment;

public class FulfilmentCalendarConfiguration : IEntityTypeConfiguration<FulfilmentCalendar>
{
    public void Configure(EntityTypeBuilder<FulfilmentCalendar> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Timezone).IsRequired().HasMaxLength(64);
        builder.Property(x => x.DeliveryDaysJson).IsRequired().HasMaxLength(256);
        // 2,048 comfortably holds the validated bound of 100 future blackout dates (§6) — a
        // request that passes validation can never fail at persistence with truncation.
        builder.Property(x => x.BlackoutDatesJson).IsRequired().HasMaxLength(2048);
        builder.Property(x => x.CutoffDayOfWeek).HasMaxLength(16);

        // One LIVE calendar per tenant (phase 1) — filtered per house convention so a
        // soft-deleted row never blocks re-authoring.
        builder.HasIndex(x => x.TenantId)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
