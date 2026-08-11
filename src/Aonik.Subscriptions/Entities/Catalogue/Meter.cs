using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Subscriptions.Entities.Catalogue;

/// <summary>
/// A named, tenant-scoped unit of entitlement (Spec 087 §6) — "stories", "animated-videos",
/// "child-profiles", "hd-styles".
///
/// This table is the <b>authority</b> for which meter codes are valid and for each meter's kind and
/// unit. Rows arrive from a business-type config pack (ADR-014) or from a module's
/// <c>IMeterDefinitionProvider</c> defaults at provisioning, and neither source is privileged.
/// Kind and unit are deliberately <b>not</b> duplicated onto <see cref="PlanEntitlement"/>: two
/// authorities for one fact let a counter meter be declared as a flag entitlement, leaving the
/// reserve, ceiling and flag paths disagreeing about the same meter.
/// </summary>
public class Meter : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Stable, tenant-unique identifier used by plans, grants and usage.</summary>
    public string Code { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>One of <c>MeterKinds</c>: counter, ceiling or flag.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Display unit, e.g. "stories". Null for flags.</summary>
    public string? Unit { get; set; }
}
