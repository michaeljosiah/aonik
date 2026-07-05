using Aonik.SharedKernel.Primitives;

namespace Aonik.PersonalFinance.Entities;

public class Subscription : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Merchant { get; set; } = string.Empty;
    public DateTime RenewalDate { get; set; }
    public decimal ExpectedAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string DetectedBy { get; set; } = string.Empty;

    // ── Commitment fields ────────────────────────────────────

    /// <summary>Billing frequency: Monthly, Weekly, Yearly, etc.</summary>
    public string Frequency { get; set; } = "Monthly";

    /// <summary>Account used to pay this subscription.</summary>
    public Guid? PaidFromAccountId { get; set; }

    /// <summary>Verification confidence: Detected, Confirmed, or Rejected.</summary>
    public string VerificationStatus { get; set; } = "Confirmed";

    /// <summary>How the system learned about this commitment: Manual, Detected, PromotedFromTransaction, Imported.</summary>
    public string Origin { get; set; } = "Manual";

    /// <summary>Detection confidence score (0.0–1.0). Null for manually created.</summary>
    public decimal? ConfidenceScore { get; set; }

    /// <summary>The transaction that triggered detection or promotion.</summary>
    public Guid? SourceTransactionId { get; set; }

    /// <summary>Optional link to a Bill entity if this subscription was previously tracked as a bill.</summary>
    public Guid? LinkedBillId { get; set; }

    /// <summary>Whether the subscription auto-renews.</summary>
    public bool Autopay { get; set; }

    // ── Classification ───────────────────────────────────────

    public string? Category { get; set; }
    public string? SubCategory { get; set; }
    public string? Notes { get; set; }

    // ── Payment tracking ─────────────────────────────────────

    /// <summary>Last time the system observed a matching transaction.</summary>
    public DateTime? LastObservedAt { get; set; }

    /// <summary>Date of the most recent charge.</summary>
    public DateTime? LastChargedAt { get; set; }

    /// <summary>Amount of the most recent charge.</summary>
    public decimal? LastChargedAmount { get; set; }
}
