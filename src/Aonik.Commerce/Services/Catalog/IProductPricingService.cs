using Aonik.Commerce.Contracts.Models.Catalog;

namespace Aonik.Commerce.Services.Catalog;

/// <summary>
/// Product pricing for the Commerce module (Spec 042 §9/§12). Commerce owns product pricing; the
/// Finance FX/fee "Pricing" subsystem is not reused.
/// </summary>
public interface IProductPricingService
{
    /// <summary>Sets the active price for a variant in a currency, superseding any prior active price.</summary>
    Task<ProductPriceDto> SetPriceAsync(SetPriceCommand command, CancellationToken cancellationToken = default);

    /// <summary>Resolves the active unit price for a variant in a currency at a point in time, or null.</summary>
    Task<decimal?> ResolvePriceAsync(Guid productVariantId, string currency, DateTime? atUtc = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// <see cref="ResolvePriceAsync"/> for a SET of variants in one bounded query — the exact same
    /// rule (active, effective window, latest EffectiveFrom wins) resolved in memory, so list
    /// surfaces never pay one pricing round trip per row. Every requested id is present in the
    /// result; a variant with no resolvable price maps to null.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, decimal?>> ResolvePricesAsync(
        IReadOnlyCollection<Guid> productVariantIds,
        string currency,
        DateTime? atUtc = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the line price of a build-your-own-box selection per the bundle's pricing mode
    /// (Fixed | SumOfComponents | SumPlusPremium). Validates the selection against the bundle's slots
    /// (Spec 042 §12).
    /// </summary>
    Task<decimal> ResolveBundlePriceAsync(
        Guid bundleProductId,
        IReadOnlyCollection<BundleSelectionLine> selection,
        string currency,
        CancellationToken cancellationToken = default);
}
