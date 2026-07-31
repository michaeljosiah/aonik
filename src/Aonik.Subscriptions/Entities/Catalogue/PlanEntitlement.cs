using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Subscriptions.Entities.Catalogue;

/// <summary>
/// What one <see cref="PlanVersion"/> confers on one <see cref="Meter"/> (Spec 087 §5, §6) —
/// "6 stories a month", "up to 3 child profiles", "HD on".
///
/// Carries no <c>Kind</c> and no <c>Unit</c>: those belong to the meter, which is the single
/// authority for them. What lives here is only what varies <i>per plan</i> — how much, and whether
/// it refreshes.
/// </summary>
public class PlanEntitlement : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid PlanVersionId { get; set; }

    /// <summary>References <see cref="Meter.Code"/>. Validated against the meter table on write.</summary>
    public string MeterCode { get; set; } = string.Empty;

    /// <summary>
    /// How much is conferred. For a ceiling this is the maximum held concurrently; for a flag,
    /// 1 for on and 0 for off.
    /// </summary>
    public decimal Allowance { get; set; }

    /// <summary>
    /// One of <c>ResetPolicies</c>. Grant expiry is derived from this rather than from the grant's
    /// source, so a <c>never</c> allowance accumulates across renewals instead of being discarded
    /// at each period end. Ignored for ceilings and flags, which are not consumed.
    /// </summary>
    public string ResetPolicy { get; set; } = string.Empty;
}
