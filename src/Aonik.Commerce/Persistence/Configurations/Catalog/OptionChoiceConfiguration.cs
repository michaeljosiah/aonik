using Aonik.Commerce.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Catalog;

public class OptionChoiceConfiguration : IEntityTypeConfiguration<OptionChoice>
{
    public void Configure(EntityTypeBuilder<OptionChoice> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Key).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Label).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Note).HasMaxLength(256);
        // Matches ProductPrice / bundle amounts so option adjustments combine with product prices
        // without rounding at a different boundary.
        builder.Property(x => x.Price).IsRequired().HasPrecision(19, 4);

        builder.HasIndex(x => new { x.TenantId, x.OptionGroupId, x.Key })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        // Two indexes share the (TenantId, OptionGroupId) property list, so they MUST be named
        // explicitly — EF keys model indexes by property list, and an unnamed second call mutates
        // the first rather than adding another. Named, they coexist: one general lookup, one
        // filtered uniqueness constraint.
        builder.HasIndex(x => new { x.TenantId, x.OptionGroupId }, "IX_AnkOptionChoices_TenantId_OptionGroupId");

        // Spec 066 §5 — the at-most-one-recommended-default invariant is enforced by the database,
        // not by service code alone: two concurrent default moves would otherwise both read the old
        // default, demote it, and promote different choices, leaving the group with two defaults
        // (and therefore non-servable). With this index one commits and the other conflicts.
        builder.HasIndex(x => new { x.TenantId, x.OptionGroupId }, "IX_AnkOptionChoices_RecommendedDefault_Unique")
            .IsUnique()
            .HasFilter("[IsRecommendedDefault] = 1 AND [IsActive] = 1 AND [IsDeleted] = 0");
    }
}
