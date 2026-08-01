using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Subscriptions.Entities.Catalogue;

/// <summary>
/// What one unit of a meter costs when bought outright (Spec 087 §12.4).
///
/// Exists because the catalogue priced <em>plans</em> and nothing else, which left
/// <c>EntitlementPurchase</c> with two bad options: trust a caller-supplied price — letting anyone
/// buy units at any amount — or be unreachable. Neither is acceptable on a money path.
///
/// Versioned and immutable once published, for the same reason as <see cref="PlanVersion"/>: the
/// version is recorded on the order line, so a later price change cannot restate a completed
/// purchase.
/// </summary>
public class MeterOffer : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string MeterCode { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public string Currency { get; set; } = string.Empty;

    /// <summary>Smallest purchasable quantity. Guards against a zero-value order.</summary>
    public decimal MinQuantity { get; set; } = 1;

    /// <summary>Largest purchasable quantity in one go, when the operator wants a ceiling on it.</summary>
    public decimal? MaxQuantity { get; set; }

    /// <summary>Monotonic per meter, starting at 1.</summary>
    public int Version { get; set; }

    public DateTime EffectiveFrom { get; set; }

    /// <summary>One of <c>MeterOfferStatuses</c>.</summary>
    public string Status { get; set; } = string.Empty;

    public DateTime? PublishedAt { get; set; }
}

/// <summary>Lifecycle of a <see cref="MeterOffer"/> (Spec 087 §12.4).</summary>
public static class MeterOfferStatuses
{
    public const string Draft = "draft";
    public const string Published = "published";

    /// <summary>Replaced by a newer published offer. Still referenced by any order line that recorded it.</summary>
    public const string Superseded = "superseded";
}
