namespace Aonik.Commerce.Contracts.Models.Sourcing;

/// <summary>One purchase-order line (Spec 053 §10). <paramref name="Quantity"/> is in the
/// ingredient's <em>base unit</em> (kg, L, each — Spec 050 §10). A null
/// <paramref name="UnitPrice"/> defaults from the supplier catalog's pack economics
/// (<c>PackPrice / PackSize</c>, per base unit); an explicit value wins over the catalog; a line
/// with neither is rejected naming the ingredient.</summary>
public record PurchaseOrderLineCommand(
    Guid IngredientId,
    decimal Quantity,
    decimal? UnitPrice = null);

/// <summary>Creates a purchase order from explicit lines (Spec 053 §10) — an <c>Order</c> with
/// <c>OrderType = "PurchaseOrder"</c> on the shared spine, never a Commerce entity. A null
/// <paramref name="Currency"/> defaults to the supplier's currency. <paramref name="Notes"/>
/// travels in the order's <c>ProvenanceJson</c>. <paramref name="IdempotencyKey"/> passes through
/// to the spine so a retried create resolves to the same order.</summary>
public record CreatePurchaseOrderCommand(
    Guid SupplierId,
    IReadOnlyList<PurchaseOrderLineCommand> Lines,
    string? Currency = null,
    string? Notes = null,
    string? IdempotencyKey = null);

/// <summary>Seeds a purchase order from Spec 052 low-stock shortfalls (Spec 053 §12). Null
/// <paramref name="AlertIds"/> = auto: every Open/Acknowledged alert for an ingredient this
/// supplier has a catalog row for. Quantity per alert = the level's <c>ReorderQuantity</c> when
/// set, else the alert-snapshot shortfall rounded up to whole packs (min one pack); unit price =
/// <c>PackPrice / PackSize</c>. The source alerts flip to <c>Ordered</c> in the same operation.</summary>
public record CreateFromShortfallCommand(
    Guid SupplierId,
    IReadOnlyList<Guid>? AlertIds = null,
    string? Notes = null,
    string? IdempotencyKey = null);
