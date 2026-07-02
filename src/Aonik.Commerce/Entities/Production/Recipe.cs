using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Production;

/// <summary>
/// The bill of materials for a finished <c>ProductVariant</c> (Spec 050 §8): which ingredients —
/// and how much of each — <see cref="YieldQuantity"/> yield-units of the product consume. At most
/// one active recipe exists per variant (service-validated and DB-enforced via a filtered unique
/// index, R3); replacing a recipe overwrites its name/yield and component rows under this same
/// audited entity (R2). Distinct from a Spec 042 Bundle: a recipe is operator master data over
/// non-saleable <c>Ingredient</c>s, never saleable variants (§9). Anemic.
/// </summary>
public class Recipe : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>The Spec 042 catalog variant this recipe produces (the recipe's "output").</summary>
    public Guid ProductVariantId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>How many yield-units one run of this recipe produces (e.g. 4 portions).</summary>
    public decimal YieldQuantity { get; set; }

    /// <summary>The yield unit, e.g. "portion".</summary>
    public string YieldUnit { get; set; } = string.Empty;

    /// <summary>One active recipe per variant (Spec 050 §8/R3).</summary>
    public bool IsActive { get; set; } = true;

    public List<RecipeComponent> Components { get; set; } = new();
}
