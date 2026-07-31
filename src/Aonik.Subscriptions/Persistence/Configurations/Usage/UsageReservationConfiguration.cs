using Aonik.Subscriptions.Entities.Usage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Subscriptions.Persistence.Configurations.Usage;

internal sealed class UsageReservationConfiguration : IEntityTypeConfiguration<UsageReservation>
{
    public void Configure(EntityTypeBuilder<UsageReservation> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SubscriberKind).IsRequired().HasMaxLength(32);
        builder.Property(x => x.MeterCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Quantity).HasPrecision(19, 4);

        // Tenant-scoped: keys are client-generated, so two tenants will collide eventually, and a
        // global constraint would fail the second against a row its query filter cannot even see.
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();

        // The sweep selector.
        builder.HasIndex(x => new { x.TenantId, x.Status, x.ExpiresAt });

        builder.HasMany(x => x.Allocations)
            .WithOne()
            .HasForeignKey(x => x.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
