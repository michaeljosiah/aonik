using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Party;

/// <summary>
/// One row per verification attempt, <strong>including failures</strong> (Spec 095 §13).
///
/// Written <em>outside</em> the enrolment transaction and committed before it is attempted, because
/// rolling this back with a failed enrolment destroys the signal it exists for — a pattern of failed
/// attempts (Spec 095 §12.2). It is keyed on the guardian plus an attempt id rather than on the
/// child, who does not exist yet and, for a failed attempt, never will.
/// </summary>
public class ConsentVerification : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>The adult being verified.</summary>
    public Guid GuardianPartyId { get; set; }

    /// <summary>
    /// Minted at the start of the enrolment flow. Correlates a successful attempt to the child
    /// created afterwards, and lets a failed one be counted with no subject at all.
    /// </summary>
    public Guid EnrolmentAttemptId { get; set; }

    /// <summary>One of <see cref="ConsentVerificationMethods"/>.</summary>
    public string Method { get; set; } = string.Empty;

    public bool Succeeded { get; set; }

    /// <summary>Outcome pointer only — never evidence we hold (Spec 095 §13).</summary>
    public string? OutcomeRef { get; set; }

    /// <summary>Why it failed, for support. Never contains the supplied credential or document.</summary>
    public string? FailureReason { get; set; }

    public DateTime AttemptedAt { get; set; }
}
