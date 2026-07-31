using Aonik.Subscriptions.Entities.Catalogue;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Subscriptions.Persistence.Configurations.Catalogue;

internal sealed class PlanVersionConfiguration : IEntityTypeConfiguration<PlanVersion>
{
    public void Configure(EntityTypeBuilder<PlanVersion> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Price).HasPrecision(19, 4);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);

        // One row per version number per plan. Version allocation reads the current maximum, so
        // two concurrent drafts would otherwise both claim the same number.
        builder.HasIndex(x => new { x.PlanId, x.Version }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Status });

        builder.HasMany(x => x.Entitlements)
            .WithOne()
            .HasForeignKey(x => x.PlanVersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
