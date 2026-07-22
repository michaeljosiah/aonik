using Aonik.Commerce.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Catalog;

public class FacetGroupConfiguration : IEntityTypeConfiguration<FacetGroup>
{
    public void Configure(EntityTypeBuilder<FacetGroup> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Key).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Label).IsRequired().HasMaxLength(128);
        builder.Property(x => x.MatchKind).IsRequired().HasMaxLength(32);
        builder.Property(x => x.SourcePath).HasMaxLength(128);
        builder.Property(x => x.OptionsJson).IsRequired().HasMaxLength(4096);

        // Unfiltered on purpose: facet groups retire via IsActive, no delete path (Spec 070 §11).
        builder.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
    }
}
