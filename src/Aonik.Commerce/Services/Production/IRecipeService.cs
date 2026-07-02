using Aonik.Commerce.Contracts.Models.Production;

namespace Aonik.Commerce.Services.Production;

/// <summary>
/// Recipe / bill-of-materials management + explosion for the Commerce module (Spec 050 §8/§11).
/// A recipe attaches to a Spec 042 <c>ProductVariant</c> and lists the ingredients — and the
/// quantity of each, in the ingredient's base unit — that <c>YieldQuantity</c> yield-units
/// consume. Explosion is the shared, read-only primitive the prep list (Spec 055), production
/// consumption (Spec 056), and food cost (Spec 051) all reuse; it never touches stock or cost.
/// </summary>
public interface IRecipeService
{
    /// <summary>
    /// Defines the active recipe for a variant or — when one already exists — replaces it in
    /// place: name/yield overwritten and component rows replaced under the same audited entity
    /// (Spec 050 R2). Never inserts a second active recipe (R3). Duplicate ingredient entries in
    /// the command are merged (quantities summed).
    /// </summary>
    Task<RecipeDto> SetRecipeAsync(SetRecipeCommand command, CancellationToken cancellationToken = default);

    /// <summary>The variant's active recipe with its components, or null when none exists.</summary>
    Task<RecipeDto?> GetRecipeAsync(Guid productVariantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Explodes the variant's active recipe: each component quantity scaled by
    /// <c>portions / YieldQuantity</c> (Spec 050 §11/R4). A variant with no active recipe returns
    /// <c>HasActiveRecipe</c> = false with empty lines — never a silent zero (R5).
    /// </summary>
    Task<RecipeExplosionDto> ExplodeAsync(Guid productVariantId, decimal portions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Explodes many variant demands and merges the result per ingredient — the whole production
    /// sheet's bill of materials (Spec 050 §11/R4). Variants without an active recipe are
    /// reported in <c>VariantsWithoutRecipe</c> (R5).
    /// </summary>
    Task<BillOfMaterialsDto> ExplodeManyAsync(IReadOnlyList<VariantDemand> demands, CancellationToken cancellationToken = default);
}
