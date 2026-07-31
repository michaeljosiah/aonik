namespace Aonik.Commerce.Contracts.Api.Catalog;

/// <summary>HTTP bodies for the Spec 067 content endpoints. Figures are per-serving decimals;
/// null = not published. Declarations/heating on VARIANTS are explicit-or-withheld — null means
/// withheld for that combination, never inherited.</summary>
/// <param name="ExpectedDefaultsSelectionJson">The standard preparation this content was
/// authored against (V-C9), from the admin read.</param>
/// <param name="ExpectedBlockSignature">The block token this replaces, or null asserting there
/// was no block (V-C10). Both are required rather than optional: an optional precondition is
/// one a non-UI caller can decline, which is the same as not having it.</param>
public record UpsertProductContentRequest(
    string ServingLabel,
    string ExpectedDefaultsSelectionJson,
    string? ExpectedBlockSignature,
    decimal? Kcal = null,
    decimal? ProteinGrams = null,
    decimal? CarbsGrams = null,
    decimal? FatGrams = null,
    decimal? FibreGrams = null,
    decimal? SugarsGrams = null,
    decimal? SaltGrams = null,
    string? Ingredients = null,
    string? Allergens = null,
    string? HeatingJson = null);

/// <param name="ExpectedCanonicalSelectionJson">The combination this content is authored FOR.
/// Required on UPDATE, where the variant already has an identity that a shifted offer would
/// move it away from; optional on ADD, where a partial selection completed by normalisation is
/// the intent for a genuinely new combination (V-C11).</param>
public record UpsertContentVariantRequest(
    string SelectionJson,
    string ServingLabel,
    string ExpectedDefaultsSelectionJson,
    string? ExpectedCanonicalSelectionJson = null,
    decimal? Kcal = null,
    decimal? ProteinGrams = null,
    decimal? CarbsGrams = null,
    decimal? FatGrams = null,
    decimal? FibreGrams = null,
    decimal? SugarsGrams = null,
    decimal? SaltGrams = null,
    string? Ingredients = null,
    string? Allergens = null,
    string? HeatingJson = null);

/// <summary>Confirming a review asserts the block still describes the standard preparation the
/// operator SAW, so that preparation travels with the request.</summary>
public record ConfirmContentReviewRequest(
    Guid ProductId,
    string ExpectedDefaultsSelectionJson);
