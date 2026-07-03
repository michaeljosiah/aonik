using Aonik.Commerce.Contracts.Models.Production;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Commerce.Services.Production;

/// <summary>
/// Production / work orders for the Commerce module (Spec 056) — the make-side counterpart of
/// checkout. Owns the guarded lifecycle (Planned → Released → InProgress → Completed | Cancelled,
/// §8) and its two stock-moving edges: <see cref="ReleaseAsync"/> consumes ingredient stock by
/// fanning the frozen per-line recipe snapshots out over Spec 052 ingredient levels in ONE
/// all-or-nothing commit (§9), and <see cref="CompleteAsync"/> optionally yields finished-good
/// stock (§10). The kitchen sheet (§11) is a pure read over the same snapshots, so what the chef
/// printed and what release draws down can never diverge.
/// </summary>
public interface IProductionOrderService
{
    /// <summary>
    /// Creates a Planned production run. Every line's variant must exist and carry an active
    /// Spec 050 recipe: the per-portion component bill is exploded once per line
    /// (<c>ExplodeAsync(variant, 1)</c>) and FROZEN onto the line as its recipe snapshot (§7/R9) —
    /// a variant without a recipe rejects the whole create, naming it, so a line never carries an
    /// empty snapshot. Duplicate variant lines are merged (quantities summed).
    /// </summary>
    Task<ProductionOrderDto> CreateAsync(CreateProductionOrderCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Seeds a Planned run from the Spec 055 production sheet for a UTC window: one line per
    /// demanded variant that has an active recipe, at its demanded portions. Sheet variants
    /// WITHOUT a recipe are skipped and surfaced in <c>SkippedVariants</c> (the sheet legitimately
    /// contains them — never a silent drop, and never a whole-seed rejection). Throws when the
    /// window holds no seedable demand.
    /// </summary>
    Task<ProductionOrderFromSheetDto> CreateFromProductionSheetAsync(CreateFromProductionSheetCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a Planned run — the consume edge (§9): the frozen snapshots are merged into one
    /// bill (per-portion × planned, summed per ingredient) and every ingredient's default-location
    /// level is drawn down, with the whole-bill availability pre-check
    /// (Available = OnHand − Reserved) and ALL decrements + the status flip committed in ONE
    /// SaveChanges — transactionally all-or-nothing on the rowversion oversell guard, with a
    /// bounded recompute-from-scratch retry on a concurrency conflict. A shortfall throws
    /// <c>InsufficientStockException</c> with nothing applied. Re-releasing a Released run is a
    /// no-op (stock is never double-consumed, R4); any other non-Planned status conflicts.
    /// </summary>
    Task<ProductionOrderDto> ReleaseAsync(Guid productionOrderId, CancellationToken cancellationToken = default);

    /// <summary>Marks a Released run as actively cooking (§8) — an optional operational sub-state
    /// with no stock effect.</summary>
    Task<ProductionOrderDto> StartAsync(Guid productionOrderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes a Released/InProgress run — the yield edge (§10): records each line's produced
    /// quantity (explicit entry, else defaulting to planned) and, when the command's
    /// <c>YieldFinishedGoods</c> is true (make-to-stock default), increments each produced
    /// variant's on-hand by that quantity via the Spec 054 signed-increment path.
    /// </summary>
    Task<ProductionOrderDto> CompleteAsync(CompleteProductionOrderCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a Planned, Released, or InProgress run (§8). Cancelling AFTER release does NOT
    /// auto-restore the consumed ingredient stock (spec Open, deferred): the raw materials
    /// physically left the shelf at release, and silently re-adding them would mask real usage —
    /// reconciliation is an explicit Spec 052 stock adjustment.
    /// </summary>
    Task<ProductionOrderDto> CancelAsync(Guid productionOrderId, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The kitchen sheet (§11): the run's header, per-dish prep detail (per-portion × planned,
    /// replayed from each line's frozen snapshot), and the merged all-ingredients totals — the
    /// printable projection whose numbers are exactly what <see cref="ReleaseAsync"/> consumes.
    /// Null when the order does not exist.
    /// </summary>
    Task<KitchenSheetDto?> GetKitchenSheetAsync(Guid productionOrderId, CancellationToken cancellationToken = default);

    /// <summary>Lists the tenant's production runs as paged summary rows (header + line count —
    /// the §11 kitchen sheet is the heavy read), optionally filtered by status, most recent
    /// planned-for first with an Id tie-break so a page walk never skips or double-counts a run.
    /// Out-of-range paging resets to the defaults (the Spec 053 list convention).</summary>
    Task<PagedResult<ProductionOrderSummaryDto>> ListAsync(string? status = null, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default);
}
