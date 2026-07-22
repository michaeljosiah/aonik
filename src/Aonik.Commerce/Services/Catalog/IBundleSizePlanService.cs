using Aonik.Commerce.Contracts.Models.Catalog;

namespace Aonik.Commerce.Services.Catalog;

/// <summary>
/// Authoring and reads for a bundle's size plan (Spec 068 §5/§11). The plan is data — presets
/// override a linear formula — and one internal calculator (<c>BoxPricing</c>) is the only code
/// that turns it into a price, shared with the cart quote and checkout so they can never disagree.
/// </summary>
public interface IBundleSizePlanService
{
    /// <summary>Full-replace upsert, validating authoring rules A1–A5 (Spec 068 §12). Presets are
    /// matched by size: same-size rows update in place, removed sizes soft-delete, new sizes
    /// insert — a price edit never churns the filtered unique index.</summary>
    Task<BoxPlanDto> UpsertAsync(Guid productId, UpsertBundleSizePlanCommand command, CancellationToken cancellationToken = default);

    /// <summary>The plan for a bundle product, or null when none is authored.</summary>
    Task<BoxPlanDto?> GetForProductAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>Public read by product slug — Active products only; null when the product is not
    /// an Active size-tiered bundle with a plan.</summary>
    Task<BoxPlanDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
