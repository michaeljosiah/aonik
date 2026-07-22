namespace Aonik.Commerce.Contracts.Models.Catalog;

// ─── Bundle size plans (Spec 068 §4/§5) ─────────────────────────────────────

public record BoxPlanPresetDto(
    int Size,
    decimal Price,
    string? Badge,
    string? Blurb,
    /// Authored display saving — never computed.
    decimal? SavingAmount,
    int SortOrder);

/// <summary>Container pricing for a size-tiered bundle: presets override the formula at their
/// size; every other size in [MinSize, MaxSize] prices as BasePrice + (size − BaseSize) ×
/// PerSpacePrice. Step 1's entire pricing UI in one read.</summary>
public record BoxPlanDto(
    Guid BundleProductId,
    int MinSize,
    int MaxSize,
    int BaseSize,
    decimal BasePrice,
    decimal PerSpacePrice,
    string Currency,
    IReadOnlyList<BoxPlanPresetDto> Presets);

public record BundleSizePresetCommand(
    int Size,
    decimal Price,
    string? Badge = null,
    string? Blurb = null,
    decimal? SavingAmount = null,
    int SortOrder = 0);

/// <summary>Full replace of a bundle's size plan (Spec 068 §11 admin surface).</summary>
public record UpsertBundleSizePlanCommand(
    int MinSize,
    int MaxSize,
    int BaseSize,
    decimal BasePrice,
    decimal PerSpacePrice,
    string Currency,
    IReadOnlyList<BundleSizePresetCommand> Presets);
