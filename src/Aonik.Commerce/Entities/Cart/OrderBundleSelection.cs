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
}
