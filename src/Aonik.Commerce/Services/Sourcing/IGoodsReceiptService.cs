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
    /// resolved-or-created under a per-tenant DB UNIQUE index BEFORE any stock/cost mutation, and
    /// is pinned to the payload (PO + normalized lines) by a stored hash. A keyed retry with the
    /// SAME payload RESUMES the receipt — re-applying only the steps whose applied-markers a
    /// post-claim crash left unset, then re-running the idempotent alert/completion tail — so the
    /// four effects apply exactly once (R7); a key reused with a different PO or different lines
    /// conflicts. Rejected: a PO that is not Pending, a line ingredient not on the PO, a
    /// non-positive quantity, a cost the Spec 051 window rules would refuse (validated BEFORE the
    /// claim), and any over-receipt (cumulative received across all non-voided receipts exceeding
    /// ordered — v1 tolerance is none), each naming the offending ingredient. Over-receipt is
    /// re-validated AFTER the claim against committed state in deterministic (CreatedAt, Id)
    /// claim order: a receipt that lost a concurrent race voids itself (audit-preserved, counted
    /// nowhere) and surfaces the conflict — as does any keyed retry of it.
    /// </summary>
    Task<GoodsReceiptDto> ReceiveAsync(ReceiveGoodsCommand command, CancellationToken cancellationToken = default);
}
