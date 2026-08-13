using Aonik.SharedKernel.Primitives;

namespace Aonik.Ai.Entities.Safety;

/// <summary>
/// A detection at a <c>Reportable</c> category, raised for a person (Spec 096 §12).
///
/// <para>
/// A durable row rather than a log line or a notification, because the thing that actually goes wrong
/// is <em>nobody looked</em> — and "nobody looked" has to be queryable. A message that failed to send
/// leaves no trace; an unacknowledged row does.
/// </para>
///
/// <para>
/// Written in the same call as the incident, not by a background job. §12 says detection at this
/// category escalates to a person <strong>immediately</strong>, and an escalation that depends on a
/// scheduler having run is not immediate.
/// </para>
/// </summary>
public class SafetyEscalation : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid SafetyIncidentId { get; set; }
    public Guid SubjectPartyId { get; set; }

    /// <summary>The category that triggered it. Always a <c>SafetyCategories.Reportable</c> value.</summary>
    public string Category { get; set; } = string.Empty;

    public DateTime RaisedAt { get; set; }

    /// <summary>
    /// Whether the material itself was actually preserved, or only the verdict.
    ///
    /// <para>
    /// <strong>False is a worse emergency than the escalation itself</strong>, and it must be said out
    /// loud rather than inferred from an absent artefact row. §12 requires preservation; if the store
    /// is unconfigured or failed, the responsible person needs to know they are acting on a record with
    /// nothing behind it — an escalation that silently implies preservation is a false assurance at the
    /// worst possible moment.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <strong>Nullable on purpose.</strong> <c>null</c> means not recorded — a legacy row, or an
    /// input block whose preservation has not resolved yet. Backfilling those as <c>false</c> would
    /// assert that evidence was lost when it may be sitting under a legal hold, corrupting the audit
    /// this column exists to clarify.
    /// </remarks>
    public bool? MaterialPreserved { get; set; }

    /// <summary>Null until a named person has actually looked. This is the field that matters.</summary>
    public DateTime? AcknowledgedAt { get; set; }

    public Guid? AcknowledgedByPartyId { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Every attempt to reach preserved material — <strong>granted and denied alike</strong> (Spec 096 §12).
///
/// <para>
/// Logging only successful access would omit the record most worth having. Somebody trying to reach
/// this material and being refused is exactly the event a later review needs to see, and it is
/// invisible if the log is written after the permission check passes.
/// </para>
/// </summary>
public class PreservedMaterialAccess : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid SafetyIncidentId { get; set; }

    /// <summary>Who asked. Never null — an unattributable access is not an access we permit.</summary>
    public Guid ActorPartyId { get; set; }

    public DateTime RequestedAt { get; set; }
    public bool WasGranted { get; set; }

    /// <summary>Why they say they need it. Recorded verbatim, unvalidated — it is evidence, not a control.</summary>
    public string Purpose { get; set; } = string.Empty;

    public string? DenialReason { get; set; }
}
