using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Cart;

/// <summary>
/// A line in a <see cref="Cart"/> (Spec 042 §11). A simple line references a single product variant.
/// A bundle line (<see cref="IsBundle"/>) references the bundle product and carries its chosen
/// contents in <see cref="Selections"/>; <see cref="UnitPriceSnapshot"/> holds the resolved bundle
/// price so a later catalog change never mutates an in-flight box. Anemic.
/// </summary>
public class CartItem : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid CartId { get; set; }

    /// <summary>For a simple line, the purchased variant. For a bundle line, the bundle product id.</summary>
    public Guid ProductVariantId { get; set; }

    public bool IsBundle { get; set; }

    /// <summary>The bundle product id when <see cref="IsBundle"/>; null otherwise.</summary>
    public Guid? BundleProductId { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPriceSnapshot { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string NameSnapshot { get; set; } = string.Empty;

    public List<CartItemSelection> Selections { get; set; } = new();
}
