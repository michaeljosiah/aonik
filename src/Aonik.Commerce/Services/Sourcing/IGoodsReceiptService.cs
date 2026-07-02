using Aonik.Commerce.Contracts.Models.Sourcing;

namespace Aonik.Commerce.Services.Sourcing;

/// <summary>
/// Goods receipt — the moment a Spec 053 purchase order becomes raw-material on-hand
/// (Spec 054 §8). Deliberately the <em>convergence point</em> of three earlier specs rather than
/// new machinery: one guarded receive increments ingredient stock (Spec 052), optionally refreshes
/// landed cost with a new effective-dated row (Spec 051), resolves recovered low-stock alerts
/// (Spec 052 + the Spec 054 recovery rule), and transitions the PO on the shared Order spine
/// (Spec 041) — <c>Complete</c> when fully received, else left <c>Pending</c> (partial receipt is
/// derived from received-vs-ordered sums, never a status — §9).
/// </summary>
public interface IGoodsReceiptService
{
    /// <summary>
    /// Receives goods against a submitted (Pending) purchase order — fully or partially (§8/§9).
    /// Idempotent by the required client-supplied <c>IdempotencyKey</c>: the key is
    /// resolved-or-created under a per-tenant DB UNIQUE index BEFORE any stock/cost mutation, so a
    /// duplicate submit — including a post-commit retry — returns the existing receipt and the four
    /// effects apply exactly once (R7). Rejected: a PO that is not Pending, a line ingredient not
    /// on the PO, a non-positive quantity, and any over-receipt (cumulative received across all
    /// receipts exceeding ordered — v1 tolerance is none), each naming the offending ingredient.
    /// </summary>
    Task<GoodsReceiptDto> ReceiveAsync(ReceiveGoodsCommand command, CancellationToken cancellationToken = default);
}
