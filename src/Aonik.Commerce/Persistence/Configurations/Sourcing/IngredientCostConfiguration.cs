using Aonik.Commerce.Entities.Sourcing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Sourcing;

public class IngredientCostConfiguration : IEntityTypeConfiguration<IngredientCost>
{
    public void Configure(EntityTypeBuilder<IngredientCost> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.UnitCost).IsRequired().HasPrecision(19, 4);

        // Date-aware "current cost at atUtc" lookup — hit for every rollup component (Spec 051 §12).
        builder.HasIndex(x => new { x.TenantId, x.IngredientId, x.Currency, x.EffectiveFrom });

        // At most one OPEN (EffectiveTo IS NULL) row per (tenant, ingredient, currency) — the
        // Spec 051 §8 concurrency guard. The service closes the prior row in the same transaction;
        // this index is the SQL Server backstop so two concurrent reprices cannot both insert a
        // current row. InMemory does not enforce filtered indexes — the service invariant is
        // covered by unit tests instead.
        builder.HasIndex(x => new { x.TenantId, x.IngredientId, x.Currency })
            .IsUnique()
            .HasFilter("[EffectiveTo] IS NULL");
    }
}
