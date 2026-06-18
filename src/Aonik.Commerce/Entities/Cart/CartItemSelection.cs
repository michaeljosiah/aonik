using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Cart;

/// <summary>
/// One chosen component of a build-your-own-box cart line (Spec 042 §12). Prices are snapshotted at
/// add/checkout. Anemic.
/// </summary>
public class CartItemSelection : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid CartItemId { get; set; }
    public Guid BundleSlotId { get; set; }
    public Guid ProductVariantId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPriceSnapshot { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string NameSnapshot { get; set; } = string.Empty;
}
