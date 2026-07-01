using Aonik.Commerce.Contracts.Models.Sourcing;

namespace Aonik.Commerce.Services.Sourcing;

/// <summary>
/// Ingredient (raw-material) master management for the Commerce module (Spec 050 §8). An
/// ingredient is what products are made from — never a saleable variant; recipes
/// (<c>IRecipeService</c>) reference ingredients by id with all quantities in the ingredient's
/// single base unit (§10).
/// </summary>
public interface IIngredientService
{
    /// <summary>Creates an ingredient. Name — and SKU, where set — must be unique per tenant.</summary>
    Task<IngredientDto> CreateAsync(CreateIngredientCommand command, CancellationToken cancellationToken = default);

    /// <summary>Updates an ingredient's master data (name, base unit, sku, category, notes, active flag).</summary>
    Task<IngredientDto> UpdateAsync(UpdateIngredientCommand command, CancellationToken cancellationToken = default);

    /// <summary>Lists the tenant's ingredients ordered by name; active only unless <paramref name="includeInactive"/>.</summary>
    Task<IReadOnlyList<IngredientDto>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>Deactivates an ingredient so new recipes cannot reference it (R1).</summary>
    Task DeactivateAsync(Guid ingredientId, CancellationToken cancellationToken = default);
}
