using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Sourcing;

/// <summary>
/// One recorded delivery against a Spec 053 purchase order (Spec 054 §7) — "goods have arrived,
/// put them away". <see cref="PurchaseOrderId"/> is a soft reference to the spine Order
/// (<c>OrderType = "PurchaseOrder"</c>, no FK); a PO may have <em>many</em> receipts (one per
/// partial delivery, §9). <see cref="IdempotencyKey"/> is the client-supplied idempotency lever:
/// it carries a per-tenant DB UNIQUE index and is resolved-or-created <em>before</em> any
/// stock/cost mutation, so a post-commit retry returns this receipt instead of double-counting
/// (§8/R7 — mirroring the Order spine's <c>CreateOrderCommand.IdempotencyKey</c>). Anemic.
/// </summary>
public class GoodsReceipt : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Soft reference to the Spec 053 purchase order (an Order on the Spec 041 spine).</summary>
    public Guid PurchaseOrderId { get; set; }

    /// <summary>Client-supplied; UNIQUE per tenant — the §8 resolve-or-create idempotency guard.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>When the goods arrived (UTC) — also the <c>EffectiveFrom</c> of any cost refresh (§10).</summary>
    public DateTime ReceivedAt { get; set; }

    public string Status { get; set; } = GoodsReceiptStatuses.Posted;

    /// <summary>
    /// SHA-256 hex (64 chars) of the purchase order id + the canonicalized normalized lines
    /// (ingredient/quantity/unit-cost, ordered), computed at claim (§8). A keyed retry must carry
    /// the SAME payload to resume; the same key with a different payload is a conflict, never a
    /// silent no-op returning a receipt for goods the caller did not describe.
    /// </summary>
    public string PayloadHash { get; set; } = string.Empty;

    /// <summary>
    /// When the §8 stock increments were applied (UTC); null = not yet. Set on the tracked receipt
    /// BEFORE the first increment so marker and stock commit atomically on the shared
    /// <c>CommerceDbContext</c> SaveChanges — a keyed retry of a receive that crashed post-claim
    /// re-runs the stock step only while this is null (resume, never double-count).
    /// </summary>
    public DateTime? StockAppliedAt { get; set; }

    /// <summary>
    /// When the §10 cost refresh was applied (UTC); null = not yet. Same marker pattern as
    /// <see cref="StockAppliedAt"/>, riding the first cost row's SaveChanges on the shared context.
    /// </summary>
    public DateTime? CostAppliedAt { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Known values for <see cref="GoodsReceipt.Status"/> (Spec 054 §7). <c>ReceiveAsync</c> is an
/// atomic single call, so v1 persists a receipt straight to <see cref="Posted"/> — the row's
/// existence under the unique <c>IdempotencyKey</c> IS the applied-once claim, and a separate
/// Draft state would only be ambiguous on retry (partially-applied effects are unknowable). A
/// Draft value can layer in later for a review-before-post workflow without touching this flow.
/// </summary>
public static class GoodsReceiptStatuses
{
    public const string Posted = "Posted";

    /// <summary>
    /// The receipt lost the post-claim over-receipt re-validation to a concurrently claimed rival
    /// (§8): it applied no stock/cost and is kept for audit only. Voided receipts are excluded from
    /// every cumulative received sum, and a keyed retry of a voided receipt surfaces the conflict
    /// rather than success — the caller must submit corrected quantities under a NEW key.
    /// </summary>
    public const string Voided = "Voided";
}
