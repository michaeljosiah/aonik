using Aonik.Commerce.Entities.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Production;

public class RecipeComponentConfiguration : IEntityTypeConfiguration<RecipeComponent>
{
    public void Configure(EntityTypeBuilder<RecipeComponent> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity).IsRequired().HasPrecision(19, 4);

        // One LIVE component row per ingredient per recipe (Spec 050 §8); duplicate submissions
        // are merged by the service. Filtered on IsDeleted because replacing a recipe (R2)
        // soft-deletes the old component rows (AonikDbContextBase converts hard deletes) — an
        // unfiltered unique index would collide with those soft-deleted predecessors on SQL
        // Server the first time a recipe is replaced.
        builder.HasIndex(x => new { x.TenantId, x.RecipeId, x.IngredientId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        // Reverse lookup: which recipes consume an ingredient (Spec 051 costing / 052 stock).
        builder.HasIndex(x => new { x.TenantId, x.IngredientId });
    }
}
