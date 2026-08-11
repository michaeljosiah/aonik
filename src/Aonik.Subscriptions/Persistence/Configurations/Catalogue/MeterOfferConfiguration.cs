using Aonik.Subscriptions.Entities.Catalogue;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Subscriptions.Persistence.Configurations.Catalogue;

internal sealed class MeterOfferConfiguration : IEntityTypeConfiguration<MeterOffer>
{
    public void Configure(EntityTypeBuilder<MeterOffer> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MeterCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.UnitPrice).HasPrecision(19, 4);
        builder.Property(x => x.MinQuantity).HasPrecision(19, 4);
        builder.Property(x => x.MaxQuantity).HasPrecision(19, 4);

        // Purchase pricing must resolve to exactly one offer; two rows for a version would make
        // "what does this cost" ambiguous on a money path.
        builder.HasIndex(x => new { x.TenantId, x.MeterCode, x.Version }).IsUnique();

        builder.HasIndex(x => new { x.TenantId, x.MeterCode, x.Status });
    }
}
