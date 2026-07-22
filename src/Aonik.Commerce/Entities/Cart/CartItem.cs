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

    /// <summary>Spec 068 §4.1 — what this line IS: a BoxDish fills a box space and is priced by the
    /// box; an AddOn (reserved, not yet writable) is an ordinary retail line beside the box. Every
    /// capacity/merge/quote rule states which kind it counts, so a second kind stays additive.</summary>
    public string LineKind { get; set; } = CartLineKinds.BoxDish;

    /// <summary>Spec 068 — the bundle slot this box line fills. Required on BoxDish lines of a box
    /// cart; part of the line's merge identity (a variant can be eligible for several slots).</summary>
    public Guid? BoxBundleSlotId { get; set; }

    /// <summary>Spec 068 — the line's exact personalisation in Spec 066 §7 canonical form,
    /// complete. String equality IS selection equality (the merge key's selection part).</summary>
    public string? PersonalisationJson { get; set; }

    /// <summary>Differs-from-default text ("Full table · Salmon"); empty string = default.</summary>
    public string? PersonalisationSummary { get; set; }

    /// <summary>Per unit, signed. Display cache re-derived on every write — never a pricing input.</summary>
    public decimal? PersonalisationAdjustment { get; set; }

    /// <summary>Product surcharge snapshot, per unit. Display cache like the adjustment.</summary>
    public decimal? UnitSurcharge { get; set; }

    public List<CartItemSelection> Selections { get; set; } = new();
}

/// <summary>Known values for <see cref="CartItem.LineKind"/> (Spec 068 §4.1). Launch ships
/// <see cref="BoxDish"/> only; <see cref="AddOn"/> is reserved so rules can already state which
/// kind they count (writes carrying it are rejected until the add-on capability lands — R13).</summary>
public static class CartLineKinds
{
    /// <summary>Fills a box space; personalisable; priced by the box, never individually.</summary>
    public const string BoxDish = "BoxDish";

    /// <summary>An ordinary retail line bought alongside the box at its own price, consuming no
    /// box space. Reserved — no write path accepts it yet.</summary>
    public const string AddOn = "AddOn";
}
