using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Services.Sourcing;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Commerce.Services.Production;

/// <summary>
/// Standard-cost rollup (Spec 051 §9): Spec 050 explosion × date-aware ingredient cost. Pure
/// composition over <see cref="IRecipeService"/> + <see cref="IIngredientCostService"/> — it holds
/// no persistence of its own and stores nothing.
/// </summary>
internal sealed class ProductCostingService : IProductCostingService
{
    private readonly IRecipeService _recipes;
    private readonly IIngredientCostService _ingredientCosts;
    private readonly IClock _clock;

    public ProductCostingService(IRecipeService recipes, IIngredientCostService ingredientCosts, IClock clock)
    {
        _recipes = recipes;
        _ingredientCosts = ingredientCosts;
        _clock = clock;
    }

    public async Task<StandardCostDto> RollupStandardCostAsync(Guid productVariantId, string currency, DateTime? atUtc = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency is required (ISO 4217, e.g. NGN).");
        }
        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        var at = atUtc ?? _clock.UtcNow;

        // Explode ONE yield-unit (REUSE Spec 050 §11) so quantities — and therefore the total —
        // are per portion (§9/R5).
        var explosion = await _recipes.ExplodeAsync(productVariantId, portions: 1m, cancellationToken);

        // No active recipe ⇒ a surfaced diagnostic, not a £0 cost (R6).
        if (!explosion.HasActiveRecipe)
        {
            return new StandardCostDto(
                productVariantId, normalizedCurrency, at,
                HasActiveRecipe: false, CostComplete: false, UnitCost: null,
                Array.Empty<ComponentCostDto>());
        }

        var lines = new List<ComponentCostDto>(explosion.Lines.Count);
        foreach (var line in explosion.Lines)
        {
            // Date-aware current cost in the requested currency (§8/§10). A cost recorded only in
            // a different currency does not match — the line is flagged, never FX-converted (R7).
            var cost = await _ingredientCosts.GetCurrentCostAsync(line.IngredientId, normalizedCurrency, at, cancellationToken);

            lines.Add(cost is null
                ? new ComponentCostDto(
                    line.IngredientId, line.IngredientName, line.BaseUnit, line.RequiredQuantity,
                    UnitCost: null, LineCost: null, HasCost: false)
                : new ComponentCostDto(
                    line.IngredientId, line.IngredientName, line.BaseUnit, line.RequiredQuantity,
                    cost.UnitCost, line.RequiredQuantity * cost.UnitCost, HasCost: true));
        }

        // Any missing cost withholds the total (UnitCost = null): a partial number presented as
        // "the" unit cost is exactly the falsely-cheap product §17 guards against (R6).
        var costComplete = lines.All(l => l.HasCost);
        decimal? unitCost = costComplete ? lines.Sum(l => l.LineCost ?? 0m) : null;

        return new StandardCostDto(
            productVariantId, normalizedCurrency, at,
            HasActiveRecipe: true, costComplete, unitCost, lines);
    }
}
