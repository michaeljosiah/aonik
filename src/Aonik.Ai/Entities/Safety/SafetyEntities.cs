using Aonik.SharedKernel.Primitives;

namespace Aonik.Ai.Entities.Safety;

/// <summary>
/// One row per <strong>attempted delivery</strong>, not only per block (Spec 096 §15).
///
/// <para>
/// Attaching run ids only to incidents — which are written when something is blocked — leaves a
/// <em>delivered</em> output with its generation and classifier runs scattered in the audit log and
/// nothing tying them to the verdict that let it through. If that delivery is later identified as a
/// false negative, by a parent or by the §10.3 evaluation, we could not reconstruct which
/// classifiers judged it safe. The audit trail would exist for everything except the failure it is
/// for.
/// </para>
///
/// <para>
/// It carries the verdict and the references, never the content: a few hundred bytes per generation,
/// and the difference between being able to answer a parent and not.
/// </para>
/// </summary>
public class SafetyDecision : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>The child the content was for.</summary>
    public Guid SubjectPartyId { get; set; }

    public string SafetyBand { get; set; } = string.Empty;
    public string Modality { get; set; } = string.Empty;

    /// <summary>Which layer decided — one of <c>SafetyLayers</c>.</summary>
    public string Layer { get; set; } = string.Empty;

    /// <summary>The <c>SafetyDecisionOutcome</c> name.</summary>
    public string Outcome { get; set; } = string.Empty;

    /// <summary>Categories that fired, comma-separated. Empty when nothing did.</summary>
    public string? Categories { get; set; }

    /// <summary>
    /// The policy version applied. Makes "why was this blocked?" answerable exactly rather than
    /// inferred from timestamps once a threshold has been changed and changed back.
    /// </summary>
    public string SafetyPolicyVersion { get; set; } = string.Empty;

    /// <summary>
    /// The generation's <c>AiRun</c>. <strong>Nullable</strong>: an L2 input block happens before
    /// dispatch, so no generation run exists — and a non-null column would force an implementation
    /// to fabricate an AI execution that never happened in order to log the block.
    /// </summary>
    public Guid? GenerationRunId { get; set; }

    /// <summary>Classifier <c>AiRun</c> ids, comma-separated. Always at least one.</summary>
    public string? ClassifierRunIds { get; set; }

    public DateTime DecidedAt { get; set; }

    /// <summary>
    /// When this row may be dropped or anonymised. A row per generation is a lot of rows about one
    /// child, so it is bounded — see <c>ISafetyRetentionSweeper</c>.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Set when the subject party is stripped on expiry, leaving the aggregate for evaluation.</summary>
    public DateTime? AnonymisedAt { get; set; }
}

/// <summary>
/// The <em>blocked</em> subset of decisions, carrying what a block additionally needs: appeal state
/// and the artefact held for it (Spec 096 §15).
/// </summary>
public class SafetyIncident : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid SafetyDecisionId { get; set; }
    public Guid SubjectPartyId { get; set; }

    /// <summary>The category that caused the block — the most severe one that fired.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// True when no guardian may view or release this (Spec 096 §8). Denormalised from the category
    /// at write time so a later policy edit cannot retroactively make a sealed incident releasable.
    /// </summary>
    public bool IsNonOverridable { get; set; }

    /// <summary>
    /// Set on a <c>Reportable</c> category. Overrides ordinary retention and any deletion request,
    /// including a subject-access erasure — which cannot be used to destroy evidence (§12).
    /// </summary>
    public bool IsUnderLegalHold { get; set; }

    /// <summary>Guardian appeal state, for the reviewable branch only.</summary>
    public string AppealState { get; set; } = SafetyAppealStates.None;

    public Guid? AppealDecidedByPartyId { get; set; }
    public DateTime? AppealDecidedAt { get; set; }

    public DateTime OccurredAt { get; set; }
}

/// <summary>
/// A short-lived pointer to blocked content, held only long enough for a guardian appeal.
///
/// <para>
/// Storing the very thing we judged unsafe for a child, indefinitely, would be perverse — so the
/// verdict is kept and the artefact is discarded. The §12 hold is the one exception.
/// </para>
/// </summary>
public class SafetyArtefact : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid SafetyIncidentId { get; set; }

    /// <summary>Storage key. Never the content itself.</summary>
    public string Reference { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    /// <summary>Skipped by the sweeper, with the skip logged, so preservation and deletion cannot silently contend.</summary>
    public bool IsUnderLegalHold { get; set; }
}

/// <summary>
/// Thresholds per band and category, as data so tuning needs no deployment, and versioned so an old
/// verdict stays attributable (Spec 096 §15).
/// </summary>
public class SafetyPolicy : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string SafetyBand { get; set; } = string.Empty;

    /// <summary>Category to confidence threshold, as JSON.</summary>
    public string ThresholdsJson { get; set; } = string.Empty;

    public bool IsActive { get; set; }
    public DateTime EffectiveFrom { get; set; }
}

public static class SafetyAppealStates
{
    public const string None = "none";

    /// <summary>A guardian asked for review. On a non-overridable category this is a SIGNAL, not a request.</summary>
    public const string Requested = "requested";

    /// <summary>Released to the child by a guardian. Reviewable categories only.</summary>
    public const string Released = "released";

    public const string Upheld = "upheld";

    /// <summary>Refused because the category is non-overridable. Recorded, and repeated use escalates.</summary>
    public const string Refused = "refused";
}
