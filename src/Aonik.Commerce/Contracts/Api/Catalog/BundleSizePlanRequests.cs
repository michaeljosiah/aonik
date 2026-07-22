namespace Aonik.Commerce.Contracts.Api.Catalog;

public record BundleSizePlanPresetRequest(
    int Size,
    decimal Price,
    string? Badge = null,
    string? Blurb = null,
    decimal? SavingAmount = null,
    int SortOrder = 0);

/// <summary>Full replace of a bundle's size plan (Spec 068 §11).</summary>
public record UpsertBundleSizePlanRequest(
    int MinSize,
    int MaxSize,
    int BaseSize,
    decimal BasePrice,
    decimal PerSpacePrice,
    string Currency,
    List<BundleSizePlanPresetRequest>? Presets);
