using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.PersonalFinance;

/// <summary>
/// A user-tracked recurring bill obligation (e.g. council tax, electricity, internet).
/// This is a personal-finance tracking entity — separate from <see cref="Bill"/>,
/// which belongs to the bill-payment execution pipeline (orders/invoices).
/// </summary>
public class PersonalRecurringBill : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid? PaidFromAccountId { get; set; }

    /// <summary>Name of the payee (e.g. "Thames Water", "BT Broadband").</summary>
    public string Payee { get; set; } = string.Empty;

    /// <summary>Billing frequency: Monthly, Weekly, Yearly, Quarterly, etc.</summary>
    public string Frequency { get; set; } = "Monthly";

    public DateTime NextDueDate { get; set; }
    public decimal? ExpectedAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool Autopay { get; set; }

    /// <summary>Lifecycle status: Active, Paused, Dormant, Cancelled, Archived.</summary>
    public string Status { get; set; } = "Active";

    // ── Commitment fields ────────────────────────────────────

    /// <summary>Verification confidence: Detected, Confirmed, or Rejected.</summary>
    public string VerificationStatus { get; set; } = "Confirmed";

    /// <summary>How the system learned about this commitment: Manual, Detected, PromotedFromTransaction, Imported.</summary>
    public string Origin { get; set; } = "Manual";

    /// <summary>Detection confidence score (0.0–1.0). Null for manually created.</summary>
    public decimal? ConfidenceScore { get; set; }

    /// <summary>The transaction that triggered detection or promotion.</summary>
    public Guid? SourceTransactionId { get; set; }

    /// <summary>Free-text description of how this bill was detected (e.g. pattern rule name).</summary>
    public string? DetectionSource { get; set; }

    // ── Classification ───────────────────────────────────────

    public string? Category { get; set; }
    public string? SubCategory { get; set; }
    public string? Notes { get; set; }

    /// <summary>External payee or account reference number.</summary>
    public string? PayeeReference { get; set; }

    // ── Reminder settings ────────────────────────────────────

    /// <summary>How many days before NextDueDate to send a reminder.</summary>
    public int? ReminderDaysBefore { get; set; }

    /// <summary>Grace period in days after due date before the bill is considered overdue.</summary>
    public int? GracePeriodDays { get; set; }

    // ── Payment tracking ─────────────────────────────────────

    /// <summary>Last time the system observed a matching transaction.</summary>
    public DateTime? LastObservedAt { get; set; }

    /// <summary>Date of the most recent payment.</summary>
    public DateTime? LastPaidAt { get; set; }

    /// <summary>Amount of the most recent payment.</summary>
    public decimal? LastPaidAmount { get; set; }
}
