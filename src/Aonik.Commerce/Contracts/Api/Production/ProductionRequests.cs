namespace Aonik.Commerce.Contracts.Api.Production;

/// <summary>HTTP request bodies for the recipe admin endpoints (Spec 050). Mapped to service commands.</summary>
public record SetRecipeRequest(
    string Name,
    decimal YieldQuantity,
    string YieldUnit,
    IReadOnlyList<SetRecipeComponentLine> Components);

/// <summary>One (ingredient, quantity) line of a set-recipe request. Quantity is in the
/// ingredient's base unit, per YieldQuantity yield-units.</summary>
public record SetRecipeComponentLine(Guid IngredientId, decimal Quantity, string? Notes);
