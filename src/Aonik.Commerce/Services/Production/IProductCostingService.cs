using Aonik.Commerce.Contracts.Models.Production;

namespace Aonik.Commerce.Services.Production;

/// <summary>
/// Standard-cost rollup for the Commerce module (Spec 051 §9): values a product variant's active
/// recipe at date-aware current ingredient cost, in exactly one currency (§10). Reuses Spec 050's
/// <c>IRecipeService.ExplodeAsync</c> — costing never traverses the BOM itself, it only adds
/// valuation. Pure, read-only compute: nothing is stored and no stock is touched; the margin
/// report (Spec 057) multiplies the result by quantity sold to get COGS.
/// </summary>
public interface IProductCostingService
{
    /// <summary>
    /// Explodes ONE yield-unit of the variant's active recipe and values each component at the
    /// ingredient's cost effective at <paramref name="atUtc"/> (default: now) in
    /// <paramref name="currency"/> (Spec 051 §9/R5). <c>UnitCost</c> is directly the cost per
    /// portion. Diagnostics are surfaced, never swallowed: no active recipe ⇒
    /// <c>HasActiveRecipe</c> = false; any component without an effective cost in the currency ⇒
    /// its line is flagged and the total is withheld (<c>CostComplete</c> = false,
    /// <c>UnitCost</c> = null) — never a silent zero or partial (R6/R7).
    /// </summary>
    Task<StandardCostDto> RollupStandardCostAsync(Guid productVariantId, string currency, DateTime? atUtc = null, CancellationToken cancellationToken = default);
}
