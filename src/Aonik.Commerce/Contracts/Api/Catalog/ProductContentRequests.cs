namespace Aonik.Commerce.Contracts.Api.Catalog;

/// <summary>HTTP bodies for the Spec 067 content endpoints. Figures are per-serving decimals;
/// null = not published. Declarations/heating on VARIANTS are explicit-or-withheld — null means
/// withheld for that combination, never inherited.</summary>
public record UpsertProductContentRequest(
    string ServingLabel,
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

public record UpsertContentVariantRequest(
    string SelectionJson,
    string ServingLabel,
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
