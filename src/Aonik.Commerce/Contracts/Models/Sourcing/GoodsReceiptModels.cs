namespace Aonik.Commerce.Contracts.Models.Sourcing;

/// <summary>One received line (Spec 054 §7). <paramref name="QuantityReceived"/> is in the
/// ingredient's <em>base unit</em> (Spec 050); the ingredient must be ON the purchase order
/// (matched against the PO line's <c>ProductId</c> soft-ref). A non-null
/// <paramref name="UnitCostActual"/> (per base unit, in the PO's currency) also refreshes the
/// ingredient's Spec 051 cost, effective from the receipt date (§10); null ⇒ stock only.</summary>
public record ReceiveGoodsLineCommand(
    Guid IngredientId,
    decimal QuantityReceived,
    decimal? UnitCostActual = null);

/// <summary>Receives goods against a submitted (Pending) purchase order (Spec 054 §8) — fully or
/// partially (§9). <paramref name="IdempotencyKey"/> is required and client-supplied: it is
/// resolved-or-created under a per-tenant DB UNIQUE index <em>before</em> any stock/cost mutation,
/// so a duplicate submit (including a post-commit retry) returns the existing receipt and the
/// effects apply exactly once (R7). A null <paramref name="ReceivedAt"/> means "now".</summary>
public record ReceiveGoodsCommand(
    Guid PurchaseOrderId,
    string IdempotencyKey,
    IReadOnlyList<ReceiveGoodsLineCommand> Lines,
    DateTime? ReceivedAt = null,
    string? Notes = null);

/// <summary>One receipt line with its derived received-vs-ordered state (Spec 054 §9).
/// <paramref name="OrderedQuantity"/> is the PO's total for the ingredient (summed across PO
/// lines); <paramref name="CumulativeReceived"/> sums every receipt for the PO — including this
/// one — so a half-delivered PO is never ambiguous. <paramref name="OnHandAfter"/> is the
/// ingredient's default-location on-hand after this receipt applied (current on-hand when the
/// receipt is returned by an idempotent retry).</summary>
public record GoodsReceiptLineDto(
    Guid Id,
    Guid IngredientId,
    string? IngredientName,
    decimal QuantityReceived,
    decimal? UnitCostActual,
    string? Currency,
    decimal OrderedQuantity,
    decimal CumulativeReceived,
    decimal OnHandAfter);

/// <summary>One goods receipt (Spec 054 §7/§8) with the outcome of the receiving flow.
/// <paramref name="PurchaseOrderCompleted"/> reports whether every PO line is now fully received
/// (the PO transitioned to <c>Complete</c>); a partial receipt leaves the PO <c>Pending</c> —
/// received-vs-ordered is derived, never a status (§9). <paramref name="ResolvedAlertIds"/> and
/// <paramref name="CostRowsWritten"/> are <em>call-scoped</em>: they report the work THIS call
/// performed, so an idempotent retry (which applies nothing) returns them empty/zero.</summary>
public record GoodsReceiptDto(
    Guid Id,
    Guid PurchaseOrderId,
    string IdempotencyKey,
    string Status,
    DateTime ReceivedAt,
    string? Notes,
    IReadOnlyList<GoodsReceiptLineDto> Lines,
    string PurchaseOrderStatus,
    bool PurchaseOrderCompleted,
    IReadOnlyList<Guid> ResolvedAlertIds,
    int CostRowsWritten);
