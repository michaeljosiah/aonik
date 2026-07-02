using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Sourcing;

/// <summary>
/// A stocked, consumable raw material — rice, tomato, a steak cut — with a single base unit of
/// measure (Spec 050 §8). Not a saleable <c>Product</c>; it is what products are made from. All
/// recipe quantities (and, in later specs, stock and cost) for this ingredient are expressed in
/// <see cref="BaseUnit"/>; there is no unit conversion in v1 (§10). Anemic.
/// </summary>
public class Ingredient : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Sku { get; set; }

    /// <summary>Base unit of measure (Spec 050 §10). An open string; see <see cref="IngredientBaseUnits"/>.</summary>
    public string BaseUnit { get; set; } = IngredientBaseUnits.Kg;

    public string? Category { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}

/// <summary>
/// Known values for <see cref="Ingredient.BaseUnit"/> (Spec 050 §10). An open string on the entity
/// so new units are additive; this is the known-values helper.
/// </summary>
public static class IngredientBaseUnits
{
    public const string Kg = "kg";
    public const string G = "g";
    public const string L = "L";
    public const string Ml = "ml";
    public const string Each = "each";
}
