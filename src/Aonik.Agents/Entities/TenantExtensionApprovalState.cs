namespace Aonik.Agents.Entities;

/// <summary>
/// Lifecycle state of a tenant-contributed agent extension (skill, MCP server, or HTTP tool),
/// per Spec 033 §7.1. A tenant author may add and test capability for their own tenant, but
/// anything that runs code, moves money, or reaches a new network destination needs platform
/// review before it becomes eligible to go live.
/// <para>
/// <c>Draft ──submit──▶ PendingPlatformReview ──approve──▶ Approved ──(TenantAdmin)──▶ Active</c>.
/// Activation is modelled separately by <c>IsActive</c> (a tenant choice once
/// <see cref="Approved"/>); editing an active extension returns it to <see cref="Draft"/> and
/// deactivates it, because an edit can change behaviour so prior approval no longer holds.
/// </para>
/// </summary>
public enum TenantExtensionApprovalState
{
    /// <summary>Newly created or edited; not yet submitted for review and never live.</summary>
    Draft,

    /// <summary>Submitted by a TenantAdmin and awaiting a PlatformAdmin decision.</summary>
    PendingPlatformReview,

    /// <summary>
    /// Reviewed and cleared by a PlatformAdmin. Eligible for a TenantAdmin to activate
    /// (<c>IsActive = true</c>); not yet necessarily live.
    /// </summary>
    Approved,

    /// <summary>Reviewed and refused by a PlatformAdmin; the rejection reason is recorded.</summary>
    Rejected,
}
