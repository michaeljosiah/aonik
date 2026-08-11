using Aonik.Subscriptions.Entities.Catalogue;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Subscriptions.Persistence.Configurations.Catalogue;

internal sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.BillingInterval).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);

        // Plans are addressed by code when subscribing; two rows sharing one would make
        // "subscribe to family" ambiguous.
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Status });

        builder.HasMany(x => x.Versions)
            .WithOne()
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
