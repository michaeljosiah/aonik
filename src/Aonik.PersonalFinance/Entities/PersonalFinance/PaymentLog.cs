using Aonik.SharedKernel.Primitives;

namespace Aonik.PersonalFinance.Entities;

/// <summary>
/// One real-world act of support (Spec 045): attributed to a CareEntity,
/// optionally honouring a commitment cycle, recorded in its <em>original</em>
/// currency (never converted), optionally corroborated by a bank transaction.
/// Simi never moves money — a PaymentLog is the capture that it happened
/// (paid by bank / Wise / cash elsewhere). Soft-delete + 30-day restore use
/// the inherited <see cref="AuditableEntity"/> <c>IsDeleted</c>/<c>DeletedAt</c>
/// (the base context auto-filters soft-deleted rows out of every query).
/// </summary>
public class PaymentLog : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>The CareEntity this act is for (Spec 043) — always attributed.</summary>
    public Guid CareEntityId { get; set; }

    /// <summary>The commitment it honours; null for one-offs like the plumber (Spec 044).</summary>
    public Guid? CommitmentId { get; set; }

    /// <summary>The specific cycle marked Paid (Spec 044 §6).</summary>
    public Guid? CommitmentCycleId { get; set; }

    // ── Money that keeps its origin (never converted) ───────────────────
    /// <summary>Positive magnitude — a log is an outflow of support.</summary>
    public decimal Amount { get; set; }

    /// <summary>ISO-4217, as actually paid.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Optional user-entered "≈ £" label only — NOT computed, no FX.</summary>
    public decimal? ApproxGbp { get; set; }

    /// <summary>Date-only semantics (DateTime, per codebase convention).</summary>
    public DateTime Date { get; set; }

    /// <summary>bank | wise | cash | other.</summary>
    public string Channel { get; set; } = "bank";

    /// <summary>manual | captureImage | captureText | captureVoice | markDone | plaidDetected.</summary>
    public string Origin { get; set; } = "manual";

    public string? Note { get; set; }

    // ── Bank corroboration (schema now; matcher is a fast-follow, §6) ────
    /// <summary>→ PersonalTransaction.Id. Unique per non-null value (dedup key).</summary>
    public Guid? SourceTransactionId { get; set; }

    /// <summary>none | matched | confirmed. One-way to confirmed; unlink → none.</summary>
    public string CorroborationStatus { get; set; } = "none";

    // ── Offline-safe create ─────────────────────────────────────────────
    /// <summary>Client-supplied; unique (TenantId, UserId, IdempotencyKey) for replay-safe sync.</summary>
    public Guid? IdempotencyKey { get; set; }
}
