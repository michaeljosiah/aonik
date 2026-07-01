using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Production;

/// <summary>
/// One ingredient line of a <see cref="Recipe"/> (Spec 050 §8): the quantity of the referenced
/// ingredient — always in that ingredient's <c>BaseUnit</c> — consumed per
/// <see cref="Recipe.YieldQuantity"/> yield-units. One component row per ingredient per recipe;
/// duplicate submissions are merged by the service. Anemic.
/// </summary>
public class RecipeComponent : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid RecipeId { get; set; }
    public Guid IngredientId { get; set; }

    /// <summary>Quantity in the ingredient's base unit, per <see cref="Recipe.YieldQuantity"/> yield-units.</summary>
    public decimal Quantity { get; set; }

    public string? Notes { get; set; }
}
