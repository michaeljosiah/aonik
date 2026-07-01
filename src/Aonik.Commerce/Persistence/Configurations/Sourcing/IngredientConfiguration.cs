using Aonik.Commerce.Entities.Sourcing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Sourcing;

public class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(128);
        builder.Property(x => x.BaseUnit).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Sku).HasMaxLength(64);
        builder.Property(x => x.Category).HasMaxLength(64);

        // Ingredient names are unique per tenant (Spec 050 §8). The service pre-checks; the
        // index guards SQL Server under concurrency.
        builder.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();

        // SKUs are unique per tenant where set (Spec 050 §8) — filtered so NULL SKUs never collide.
        builder.HasIndex(x => new { x.TenantId, x.Sku })
            .IsUnique()
            .HasFilter("[Sku] IS NOT NULL");
    }
}
