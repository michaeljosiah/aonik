using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.PersonalFinance;

public class DebtRepayment : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid? PaidFromAccountId { get; set; }

    /// <summary>Name of the lender or creditor (e.g. "Halifax", "Klarna").</summary>
    public string CreditorName { get; set; } = string.Empty;

    /// <summary>
    /// Debt classification: Mortgage, PersonalLoan, StudentLoan, AutoLoan,
    /// CreditCardRepayment, BNPL, Other.
    /// </summary>
    public string DebtType { get; set; } = string.Empty;

    public DateTime NextDueDate { get; set; }
    public decimal? ExpectedAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Frequency { get; set; } = "Monthly";
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

    // ── Classification ───────────────────────────────────────

    public string? Notes { get; set; }

    /// <summary>External account or loan reference number.</summary>
    public string? AccountReference { get; set; }

    // ── Payment tracking ─────────────────────────────────────

    /// <summary>Last time the system observed a matching transaction.</summary>
    public DateTime? LastObservedAt { get; set; }

    /// <summary>Date of the most recent payment.</summary>
    public DateTime? LastPaidAt { get; set; }

    /// <summary>Amount of the most recent payment.</summary>
    public decimal? LastPaidAmount { get; set; }
}
