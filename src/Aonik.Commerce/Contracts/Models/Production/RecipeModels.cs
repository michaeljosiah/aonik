namespace Aonik.Commerce.Contracts.Models.Production;

/// <summary>One (ingredient, quantity) line of a <see cref="SetRecipeCommand"/> (Spec 050 §8).
/// Quantity is in the ingredient's base unit, per <c>YieldQuantity</c> yield-units. Duplicate
/// ingredient entries in one command are merged by the service (quantities summed).</summary>
public record RecipeComponentCommand(Guid IngredientId, decimal Quantity, string? Notes = null);

/// <summary>Defines — or replaces, in place, under the same audited entity (Spec 050 R2) — the
/// active recipe for a product variant.</summary>
public record SetRecipeCommand(
    Guid ProductVariantId,
    string Name,
    decimal YieldQuantity,
    string YieldUnit,
    IReadOnlyList<RecipeComponentCommand> Components);

public record RecipeComponentDto(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    string BaseUnit,
    decimal Quantity,
    string? Notes);

public record RecipeDto(
    Guid Id,
    Guid ProductVariantId,
    string Name,
    decimal YieldQuantity,
    string YieldUnit,
    bool IsActive,
    IReadOnlyList<RecipeComponentDto> Components);

/// <summary>One exploded requirement line: how much of an ingredient (in its base unit) is
/// required (Spec 050 §11).</summary>
public record ExplodedLineDto(
    Guid IngredientId,
    string IngredientName,
    string BaseUnit,
    decimal RequiredQuantity);

/// <summary>The exploded bill of materials for one variant at a portion count (Spec 050 §11/R4).
/// <c>HasActiveRecipe</c> = false (with empty lines) flags "no recipe defined" — never a silent
/// zero (R5).</summary>
public record RecipeExplosionDto(
    Guid ProductVariantId,
    decimal Portions,
    bool HasActiveRecipe,
    IReadOnlyList<ExplodedLineDto> Lines);

/// <summary>Demand for one variant, in portions (yield-units) — an input line of
/// <c>ExplodeManyAsync</c> (Spec 050 §11).</summary>
public record VariantDemand(Guid ProductVariantId, decimal Portions);

/// <summary>A merged bill of materials across variants (Spec 050 §11/R4): required quantity summed
/// per ingredient, plus the variants that have no active recipe (R5).</summary>
public record BillOfMaterialsDto(
    IReadOnlyList<ExplodedLineDto> Lines,
    IReadOnlyList<Guid> VariantsWithoutRecipe);
