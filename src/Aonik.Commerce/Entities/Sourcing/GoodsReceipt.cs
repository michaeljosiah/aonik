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
}
