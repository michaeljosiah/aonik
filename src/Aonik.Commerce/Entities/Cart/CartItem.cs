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
    /// box; an AddOn is an ordinary retail line beside the box, priced at its own snapshot and
    /// consuming no capacity. Every capacity/merge/quote rule states which kind it counts, which is
    /// what let Spec 071 activate AddOn additively rather than by revisiting those rules.</summary>
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

/// <summary>Known values for <see cref="CartItem.LineKind"/> (Spec 068 §4.1). Spec 068 shipped
/// <see cref="BoxDish"/> and reserved <see cref="AddOn"/> so every capacity/merge/quote rule
/// already stated which kind it counted; Spec 071 then activated <see cref="AddOn"/> without
/// revisiting them. Both kinds are live. R13's classify-as-<see cref="BoxDish"/> rule is the
/// column's DATABASE DEFAULT, not a read-time fallback: it covers rows that predate the column
/// and inserts that omit it. The column is NOT NULL, so an explicit null is rejected rather
/// than normalised, and nothing coalesces one on materialisation.</summary>
public static class CartLineKinds
{
    /// <summary>Fills a box space; personalisable; priced by the box, never individually.</summary>
    public const string BoxDish = "BoxDish";

    /// <summary>An ordinary retail line bought alongside the box at its own retail price snapshot,
    /// consuming no box capacity — the deliberate exception to the storefront's no-standalone-dish-
    /// price rule. Added via <c>POST /commerce/carts/{cartId}/extras</c> (Spec 071); contributes the
    /// quote's <c>addOns</c> component and materialises as one ordinary retail order item per line
    /// at checkout.</summary>
    public const string AddOn = "AddOn";
}
