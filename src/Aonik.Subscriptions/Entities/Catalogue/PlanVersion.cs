using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Subscriptions.Entities.Catalogue;

/// <summary>
/// One priced, entitled revision of a <see cref="Plan"/> (Spec 087 §6). A subscription pins a
/// version id, which is what makes grandfathering real: raising a price mints a new version and
/// leaves existing subscribers on the one they agreed to.
///
/// <b>Once published, price and entitlements are immutable.</b> Pinning a version grandfathers
/// nothing if the pinned row stays editable — an admin edit would silently re-price and reshape
/// every subscriber on it. Changes after publication require a new version; only lifecycle
/// metadata may move afterwards. The invariant is enforced by the catalogue service, not by a
/// convention.
/// </summary>
public class PlanVersion : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid PlanId { get; set; }

    /// <summary>Monotonic per plan, starting at 1.</summary>
    public int Version { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTime EffectiveFrom { get; set; }

    /// <summary>One of <c>PlanVersionStatuses</c>: draft, published or superseded.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Set when the version was published; null while it is still a draft.</summary>
    public DateTime? PublishedAt { get; set; }

    public List<PlanEntitlement> Entitlements { get; set; } = new();
}
