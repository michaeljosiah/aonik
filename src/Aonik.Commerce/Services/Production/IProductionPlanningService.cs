using Aonik.Commerce.Contracts.Models.Production;

namespace Aonik.Commerce.Services.Production;

/// <summary>
/// Production planning for the Commerce module (Spec 055): the two artefacts the operations
/// manager opens each morning, both <strong>projections computed on read</strong> — no new
/// persisted entity, no migration. The production sheet aggregates <c>ProductPurchase</c> demand
/// by variant over a window (read through the SharedKernel Ordering contract — never a Finance
/// reference); the prep list is that sheet exploded through Spec 050 recipes, optionally netted
/// against Spec 052 available stock.
/// </summary>
public interface IProductionPlanningService
{
    /// <summary>
    /// Per-variant portion demand aggregated from <c>ProductPurchase</c> orders whose
    /// <c>CreatedAt</c> falls in <c>[window.FromUtc, window.ToUtc)</c> and whose status is in the
    /// §9 demand set (committed, non-terminal-failed: Pending, UnderReview, Approved, Transmitted,
    /// Complete — never Draft, Cancelled, Failed, or Expired). Build-your-own-box lines are
    /// expanded into their chosen component variants via <c>OrderBundleSelection</c> (Spec 042
    /// §12 Option A) — the components are the real kitchen demand.
    /// </summary>
    Task<ProductionSheetDto> GetProductionSheetAsync(ProductionWindow window, CancellationToken cancellationToken = default);

    /// <summary>
    /// The production sheet fed through <c>IRecipeService.ExplodeManyAsync</c> (Spec 050 §11) —
    /// per-ingredient required quantity in base units, merged across variants, with the no-recipe
    /// diagnostic surfaced. When <paramref name="netAgainstStock"/> is true (default), each line is
    /// netted against Spec 052's <c>Available = OnHand − Reserved</c> (never raw on-hand) to add
    /// <c>Shortfall</c> and <c>SuggestedOrderQuantity</c>; when false the netting fields are null
    /// and the list is pure requirements.
    /// </summary>
    Task<PrepListDto> GetPrepListAsync(ProductionWindow window, bool netAgainstStock = true, CancellationToken cancellationToken = default);
}
