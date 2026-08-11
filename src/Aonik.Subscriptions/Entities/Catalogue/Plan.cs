using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Subscriptions.Entities.Catalogue;

/// <summary>
/// What is sold (Spec 087 §6). A plan is the stable, customer-facing identity — "Family" — while
/// its price and entitlements live on <see cref="PlanVersion"/>, so they can change without
/// re-pricing anyone already subscribed.
/// </summary>
public class Plan : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Stable, tenant-unique identifier used when subscribing.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>One of <c>BillingIntervals</c>. An open string; new intervals are additive.</summary>
    public string BillingInterval { get; set; } = string.Empty;

    /// <summary>One of <c>PlanStatuses</c>: draft, active or retired.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Display order on a pricing page. Presentation only.</summary>
    public int SortOrder { get; set; }

    public List<PlanVersion> Versions { get; set; } = new();
}
