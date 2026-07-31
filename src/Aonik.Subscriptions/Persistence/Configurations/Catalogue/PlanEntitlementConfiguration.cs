using Aonik.Subscriptions.Entities.Catalogue;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Subscriptions.Persistence.Configurations.Catalogue;

internal sealed class PlanEntitlementConfiguration : IEntityTypeConfiguration<PlanEntitlement>
{
    public void Configure(EntityTypeBuilder<PlanEntitlement> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MeterCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Allowance).HasPrecision(19, 4);
        builder.Property(x => x.ResetPolicy).IsRequired().HasMaxLength(32);

        // Two entitlements for one meter on one version would DOUBLE the materialised allowance at
        // every renewal — the most expensive of the catalogue duplicates, and silent.
        builder.HasIndex(x => new { x.PlanVersionId, x.MeterCode }).IsUnique();
    }
}
