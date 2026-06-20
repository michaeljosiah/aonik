using Aonik.SharedKernel.Primitives;

namespace Aonik.Ai.Entities;

/// <summary>
/// A tenant-scoped, optionally segment-scoped aggregate of "what has worked" for a kind of decision
/// (Spec 041, Addition B). Unlike <see cref="UserMemoryEntry"/> (which is per-user), a pattern answers
/// "for this decision type, what approach has worked across this tenant/segment's cases?". It is
/// reinforced as outcomes confirm it and superseded — never deleted — when outcomes contradict it
/// (a reversal is itself useful signal). It is never user-scoped, never global, never cross-tenant.
/// Anemic per the AONIK entity rule: behaviour lives in <c>DecisionPatternService</c>.
/// </summary>
public class DecisionPattern : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Decision family, e.g. "dunning", "remittance-routing", "collections".</summary>
    public string DecisionType { get; set; } = string.Empty;

    /// <summary>Optional sub-scope within the tenant, e.g. "small-business/low-risk". Null = tenant-wide.</summary>
    public string? Segment { get; set; }

    /// <summary>Human-readable distilled pattern ("a soft reminder at day 3 cleared most invoices").</summary>
    public string Statement { get; set; } = string.Empty;

    /// <summary>Optional structured detail backing the statement (cadence, thresholds, …). IDs/references, no PII.</summary>
    public string? PayloadJson { get; set; }

    /// <summary>How many resolved cases have reinforced this pattern.</summary>
    public int ObservationCount { get; set; }

    /// <summary>0–1 confidence, bounded and monotonic only within the current (non-superseded) pattern.</summary>
    public decimal Confidence { get; set; }

    public DateTime? LastReinforcedAtUtc { get; set; }

    /// <summary>Set when a contradicting trend supersedes this pattern; a new current pattern starts instead.</summary>
    public DateTime? SupersededAtUtc { get; set; }
}
