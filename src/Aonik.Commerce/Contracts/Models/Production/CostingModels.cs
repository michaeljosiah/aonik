namespace Aonik.Commerce.Contracts.Models.Production;

/// <summary>One costed component line of a standard-cost rollup (Spec 051 §9): the per-yield-unit
/// quantity from the Spec 050 explosion, valued at the ingredient's date-aware current cost in the
/// requested currency. A component with no effective cost in that currency is flagged
/// (<c>HasCost</c> = false, <c>LineCost</c> = null) — never a silent zero (R6/R7).</summary>
public record ComponentCostDto(
    Guid IngredientId,
    string IngredientName,
    string BaseUnit,
    decimal QuantityPerYieldUnit,
    decimal? UnitCost,
    decimal? LineCost,
    bool HasCost);

/// <summary>A product variant's standard cost — its active recipe valued at current ingredient
/// costs (Spec 051 §9). <c>UnitCost</c> is the cost per portion (per yield-unit) and is
/// <strong>null</strong> when the variant has no active recipe or any component lacks a cost
/// (<c>CostComplete</c> = false) — the total is withheld, never a silent zero or partial (R6).
/// Computed fresh from the recipe and costs effective at <c>AsOfUtc</c>; never stored.</summary>
public record StandardCostDto(
    Guid ProductVariantId,
    string Currency,
    DateTime AsOfUtc,
    bool HasActiveRecipe,
    bool CostComplete,
    decimal? UnitCost,
    IReadOnlyList<ComponentCostDto> Lines);
