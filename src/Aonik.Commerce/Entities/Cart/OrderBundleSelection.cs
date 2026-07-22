using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Cart;

/// <summary>
/// Commerce-owned record of a build-your-own-box order line's chosen contents (Spec 042 §12,
/// Option A). The order records the box as a single line; this captures what went in it, soft-linked
/// by <see cref="OrderId"/> + <see cref="OrderItemIndex"/> (no FK into the Order spine). Inventory
/// commit and pick/pack read this. Anemic.
/// </summary>
public class OrderBundleSelection : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public int OrderItemIndex { get; set; }
    public Guid BundleSlotId { get; set; }
    public Guid ProductVariantId { get; set; }
    public decimal Quantity { get; set; }
    public string Sku { get; set; } = string.Empty;

    /// <summary>Spec 068 §9 — canonical selection (query convenience; projection of the envelope).</summary>
    public string? PersonalisationJson { get; set; }

    /// <summary>Differs-from-default text (query convenience); "" = default preparation.</summary>
    public string? PersonalisationSummary { get; set; }

    /// <summary>Per unit, signed (query convenience).</summary>
    public decimal? PersonalisationAdjustment { get; set; }

    /// <summary>Per unit (query convenience).</summary>
    public decimal? UnitSurcharge { get; set; }

    /// <summary>The complete Spec 066 §12 personalisation envelope for this selection row —
    /// selection, summary, label-snapshotted display, isDefault, adjustment, currency, per-group
    /// breakdown, surcharge + its currency. Immutable full-fidelity record: after defaults or
    /// option prices change, the order must still explain which choices produced the amount
    /// charged without consulting the live catalogue. The scalar columns above are projections.</summary>
    public string? PersonalisationEnvelopeJson { get; set; }
}
