using Aonik.Subscriptions.Entities.Catalogue;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Subscriptions.Persistence.Configurations.Catalogue;

internal sealed class MeterConfiguration : IEntityTypeConfiguration<Meter>
{
    public void Configure(EntityTypeBuilder<Meter> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(100);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Kind).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Unit).HasMaxLength(50);

        // The meter table is the validator for every MeterCode written anywhere in this module,
        // so an ambiguous code would make validation itself ambiguous. Concurrent admin writes, a
        // re-run provisioning contributor and an overlapping config pack can each produce a
        // duplicate; the constraint belongs in storage rather than in a service check.
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}
