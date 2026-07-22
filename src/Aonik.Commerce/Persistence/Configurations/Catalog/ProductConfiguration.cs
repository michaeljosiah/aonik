using Aonik.Commerce.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Catalog;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Slug).IsRequired().HasMaxLength(160);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Description).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Kind).IsRequired().HasMaxLength(32);
        builder.Property(x => x.TagsJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.AttributesJson).HasColumnType("nvarchar(max)");

        builder.Property(x => x.BundlePricingMode).HasMaxLength(32);
        builder.Property(x => x.BundleCurrency).HasMaxLength(3);
        builder.Property(x => x.BundleFixedAmount).HasPrecision(19, 4);
        builder.Property(x => x.BundlePremium).HasPrecision(19, 4);

        // Spec 057 — target gross-margin percentage (0–100, two decimal places); range enforced in
        // MarginReportService.SetTargetMarginAsync (InMemory tests cannot prove DB precision).
        builder.Property(x => x.TargetMarginPct).HasPrecision(5, 2);

        // Spec 066 — per-unit surcharge, carried with its own denomination so it can never be
        // silently re-labelled by a storefront currency change.
        builder.Property(x => x.UnitSurcharge).HasPrecision(19, 4);
        builder.Property(x => x.UnitSurchargeCurrency).HasMaxLength(3);

        // Spec 070 §7 — hidden search keywords. 1024 is an authoring bound, not a search-engine
        // substitute. The database default backfills existing rows with a valid empty array when
        // the column is added, so no row ever holds a non-JSON empty string.
        builder.Property(x => x.SearchKeywordsJson)
            .IsRequired()
            .HasMaxLength(1024)
            .HasDefaultValue("[]");

        builder.HasMany(x => x.Variants)
            .WithOne()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Media)
            .WithOne()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.BundleSlots)
            .WithOne()
            .HasForeignKey(x => x.BundleProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.Slug }).IsUnique();
        builder.HasIndex(x => x.CategoryId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Kind);
    }
}
