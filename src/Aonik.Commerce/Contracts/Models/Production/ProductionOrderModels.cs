namespace Aonik.Commerce.Contracts.Models.Production;

// Spec 056 — production / work orders + the kitchen sheet. A production order is an internal
// Commerce work order (never an Order on the Spec 041 spine): release consumes ingredient stock
// through the frozen per-line recipe snapshots (§9), completion optionally yields finished-good
// stock (§10), and the kitchen sheet is the printable projection of the same snapshots (§11).

/// <summary>One dish to produce: a Spec 042 variant and the portions to make (Spec 056 §7).
/// Duplicate variant entries in one command are merged by the service (quantities summed).</summary>
public record ProductionOrderLineCommand(Guid ProductVariantId, decimal PlannedQuantity);

/// <summary>Creates a production run (Spec 056 §7/§8). Every line's variant must exist and carry
/// an active Spec 050 recipe — the per-portion snapshot is frozen per line at creation, so a line
/// can never hold an empty component bill (§7/R9).</summary>
public record CreateProductionOrderCommand(
    DateTime PlannedFor,
    IReadOnlyList<ProductionOrderLineCommand> Lines,
    string? Notes = null);

/// <summary>Seeds a production run from the Spec 055 production sheet for a UTC window (half-open
/// [FromUtc, ToUtc)). Sheet variants WITHOUT an active recipe are skipped and reported — the sheet
/// legitimately contains them, so they surface as <c>SkippedVariants</c>, never a silent drop.
/// <see cref="PlannedFor"/> defaults to <see cref="FromUtc"/>.</summary>
public record CreateFromProductionSheetCommand(
    DateTime FromUtc,
    DateTime ToUtc,
    DateTime? PlannedFor = null,
    string? Notes = null);

/// <summary>The actual portions a line produced (Spec 056 §10); 0 records a failed batch.</summary>
public record ProducedQuantityLine(Guid ProductionOrderLineId, decimal ProducedQuantity);

/// <summary>Completes a run (Spec 056 §10): records each line's produced quantity (explicit entry,
/// else defaulting to planned) and — when <see cref="YieldFinishedGoods"/> is true, the
/// make-to-stock default — increments each produced variant's on-hand by that quantity. Off for
/// make-to-order tenants whose portions must not re-enter sellable stock.</summary>
public record CompleteProductionOrderCommand(
    Guid ProductionOrderId,
    IReadOnlyList<ProducedQuantityLine>? ActualQuantities = null,
    bool YieldFinishedGoods = true);

/// <summary>
/// One frozen component of a line's recipe snapshot (Spec 056 §7/R9): the ingredient and how much
/// of it ONE portion consumes, in its base unit — Spec 050's <c>ExplodeAsync(variant, 1)</c>
/// captured at creation. Scale-invariant: the kitchen sheet and release both multiply by the
/// line's planned portions, so the two can never disagree.
/// </summary>
public record RecipeSnapshotComponent(
    Guid IngredientId,
    string IngredientName,
    string BaseUnit,
    decimal QuantityPerPortion);

public record ProductionOrderLineDto(
    Guid Id,
    Guid ProductVariantId,
    decimal PlannedQuantity,
    decimal? ProducedQuantity,
    IReadOnlyList<RecipeSnapshotComponent> RecipeSnapshot);

public record ProductionOrderDto(
    Guid Id,
    DateTime PlannedFor,
    string Status,
    string? Notes,
    DateTime? ReleasedAt,
    DateTime? CompletedAt,
    IReadOnlyList<ProductionOrderLineDto> Lines);

/// <summary>One row of the production-run list: the run's header plus a line count, mirroring the
/// spine's <c>OrderSummary</c> (the Spec 053 purchase-order list convention). The frozen per-line
/// snapshots are the heavy payload and belong to the §11 kitchen sheet, never the board list.</summary>
public record ProductionOrderSummaryDto(
    Guid Id,
    DateTime PlannedFor,
    string Status,
    string? Notes,
    DateTime? ReleasedAt,
    DateTime? CompletedAt,
    int LineCount);

/// <summary>A from-sheet seed's result (Spec 056 §7): the created run plus the demanded variants
/// that were skipped because they carry no active recipe — reported, never silently dropped.</summary>
public record ProductionOrderFromSheetDto(
    ProductionOrderDto Order,
    IReadOnlyList<Guid> SkippedVariants);

/// <summary>One prep-detail line of a kitchen-sheet dish (Spec 056 §11), replayed from the frozen
/// snapshot: per-portion quantity and the line total (per-portion × planned portions).</summary>
public record KitchenSheetComponentDto(
    Guid IngredientId,
    string IngredientName,
    string BaseUnit,
    decimal QuantityPerPortion,
    decimal RequiredQuantity);

/// <summary>One dish of the kitchen sheet (Spec 056 §11). Names are resolved from the live catalog
/// with LEFT-join semantics — a variant that no longer resolves still shows, with diagnostic
/// placeholder names, never dropped.</summary>
public record KitchenSheetDishDto(
    Guid ProductionOrderLineId,
    Guid ProductVariantId,
    string ProductName,
    string VariantName,
    decimal PlannedQuantity,
    decimal? ProducedQuantity,
    IReadOnlyList<KitchenSheetComponentDto> Components,
    /// Spec 068 §9 — how these portions are prepared ("Full table · Salmon"); null when the
    /// demand carried no personalisation.
    string? PersonalisationSummary = null,
    /// Label-snapshotted Spec 066 §12 display entries, frozen at materialisation (raw JSON).
    string? PersonalisationDisplayJson = null);

/// <summary>One line of the kitchen sheet's merged all-ingredients totals (Spec 056 §11) — the
/// shopping/prep summary, summed across dishes from the same frozen snapshots.</summary>
public record KitchenSheetTotalLineDto(
    Guid IngredientId,
    string IngredientName,
    string BaseUnit,
    decimal RequiredQuantity);

/// <summary>
/// The kitchen sheet (Spec 056 §11): the printable projection a chef works from — the run's
/// header, per-dish prep detail, and a merged totals bill. Assembled from the SAME frozen per-line
/// snapshots release consumes (§9), so the numbers on the pass are byte-for-byte what release
/// draws down, even if the live Spec 050 recipe was edited after creation. Rendering to
/// PDF/print is a frontend concern.
/// </summary>
public record KitchenSheetDto(
    Guid ProductionOrderId,
    DateTime PlannedFor,
    string Status,
    string? Notes,
    IReadOnlyList<KitchenSheetDishDto> Dishes,
    IReadOnlyList<KitchenSheetTotalLineDto> Totals);
