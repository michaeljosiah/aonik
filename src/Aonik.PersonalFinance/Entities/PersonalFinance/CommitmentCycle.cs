using Aonik.SharedKernel.Primitives;

namespace Aonik.PersonalFinance.Entities;

/// <summary>
/// One occurrence of a commitment's rhythm (Spec 044 §6) — the only honest way
/// to represent "paid / skipped / snoozed" history and compute "never missed."
/// At most one <c>Open</c> cycle per commitment at a time (the current one);
/// resolving it (paid/skipped) opens the next via <c>Rhythm.NextAfter</c>.
/// History is append-only — editing a commitment never rewrites past cycles.
/// </summary>
public class CommitmentCycle : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>→ PersonalRecurringBill.Id.</summary>
    public Guid CommitmentId { get; set; }

    /// <summary>Date-only semantics (DateTime, per codebase convention).</summary>
    public DateTime DueDate { get; set; }

    /// <summary>Open | Paid | Skipped | Snoozed.</summary>
    public string Status { get; set; } = "Open";

    /// <summary>Set when Paid — the PaymentLog that honoured this cycle (Spec 045).</summary>
    public Guid? PaymentLogId { get; set; }

    public string? SkipReason { get; set; }
    public DateTime? SnoozedUntil { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
