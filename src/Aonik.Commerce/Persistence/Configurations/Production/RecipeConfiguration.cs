using Aonik.Commerce.Entities.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Production;

public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(128);
        builder.Property(x => x.YieldUnit).IsRequired().HasMaxLength(32);
        builder.Property(x => x.YieldQuantity).IsRequired().HasPrecision(19, 4);

        builder.HasMany(x => x.Components)
            .WithOne()
            .HasForeignKey(x => x.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        // One ACTIVE recipe per variant (Spec 050 §8/R3): two concurrent SetRecipe calls cannot
        // each insert an active recipe — the losing writer surfaces a domain error, never a
        // second active recipe. Service validation pairs with this (InMemory tests prove the
        // service; the index guards SQL Server).
        builder.HasIndex(x => new { x.TenantId, x.ProductVariantId })
            .IsUnique()
            .HasFilter("[IsActive] = 1");
    }
}
