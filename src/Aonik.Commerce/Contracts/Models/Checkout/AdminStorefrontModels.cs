using Aonik.Commerce.Services.Checkout;

namespace Aonik.Commerce.Contracts.Models.Checkout;

// ─── Admin storefront projections (Specs 073/081/083 dependency endpoints) ──
// Read-only shapes over data Specs 068/071/072 already persist. Nothing here
// ever serializes a cart token (R10), and nothing here mutates state — the
// availability/price flags on the cart detail are computed read-only, never
// persisted (drift REPAIR remains the customer load path's job).

/// <summary>One row of the tenant-wide storefront order list — the Spec 083
/// "list projection": payment/fulfilment statuses and buyer kind the spine's
/// generic list cannot supply.</summary>
public record AdminStorefrontOrderRowDto(
    Guid OrderId,
    /// "party" | "guest" — from the checked-out cart's buyer binding.
    string BuyerKind,
    Guid? BuyerPartyId,
    DateTime PlacedAtUtc,
    string OrderStatus,
    /// From the durable OrderChargeSummary (checkout's funding record).
    string PaymentStatus,
    /// Derived from the spine status: Fulfilled / Cancelled / Unfulfilled.
    string FulfilmentStatus,
    string Currency,
    decimal Total,
    int? BoxSize);

public record AdminOrderStorefrontItemDto(
    string ItemType,
    string Name,
    string? Sku,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal Amount,
    /// Spec 071 — an ordinary retail line sold alongside the box.
    bool IsAddOn,
    bool IsDeliveryFee);

public record AdminOrderChargeDto(
    decimal Subtotal,
    decimal DiscountTotal,
    string? DiscountCode,
    decimal TaxTotal,
    decimal Total,
    string Currency);

/// <summary>The ENTIRE storefront order detail (Spec 083 dependency callout 1):
/// the spine's generic read is bill-payment-shaped, so this projection is the
/// only admin view that can name the box aggregate, its kitchen landing, the
/// add-ons and the charge envelope.</summary>
public record AdminOrderStorefrontDto(
    Guid OrderId,
    string BuyerKind,
    Guid? BuyerPartyId,
    DateTime PlacedAtUtc,
    string OrderStatus,
    string PaymentStatus,
    string FulfilmentStatus,
    IReadOnlyList<AdminOrderStorefrontItemDto> Items,
    /// The kitchen landing — per-selection personalisation snapshots.
    IReadOnlyList<StorefrontOrderSelectionDto> Selections,
    AdminOrderChargeDto Charge,
    int? BoxSize);

/// <summary>Box state on a cart row (Spec 083 list contract). <see cref="Drift"/>
/// is the computed, never-persisted "checkout blocked" signal for OPEN box
/// carts — any line unavailable (retired/deactivated catalogue state, vanished
/// add-on price, or cart-wide demand over available stock) or any add-on price
/// changed against its snapshot. Frozen carts always report false: their
/// snapshots are the recorded truth, not a live session to re-validate.</summary>
public record AdminCartBoxMetaDto(int Size, int Filled, bool Drift);

/// <summary>Carts admin list row (Spec 083 dependency callout 2).
/// <see cref="Total"/> is the recorded charge total once checked out; for open
/// carts it is the box GOODS value — box price + personalisation + surcharges +
/// add-on snapshots — delivery/discount/tax are checkout-time facts.</summary>
public record AdminCartRowDto(
    Guid CartId,
    string BuyerKind,
    Guid? BuyerPartyId,
    string Status,
    string Currency,
    decimal ItemCount,
    decimal Total,
    AdminCartBoxMetaDto? BoxMeta,
    Guid? OrderId,
    DateTime UpdatedAtUtc);

public record AdminCartLineDto(
    Guid LineId,
    string Kind,
    string Name,
    string Sku,
    decimal Quantity,
    decimal UnitPriceSnapshot,
    string? PersonalisationSummary,
    /// The STORED canonical selection (Spec 066 §7) — the structured form of the
    /// line's preparation; null when the line is unpersonalised.
    string? SelectionJson,
    /// Computed read-only, live editable carts only: the variant/product is
    /// retired or deactivated, an add-on's retail price vanished, cart-wide
    /// demand for the variant exceeds available DEFAULT-location stock right
    /// now (a missing stock row is ZERO, exactly like the box path), or the
    /// selection can no longer be priced. Nothing is persisted; the customer's
    /// next load runs the real Spec 068 repair.
    bool IsUnavailable,
    /// The line's charge would change on the customer's next continue: the
    /// add-on retail price moved against its snapshot, or the renormalised
    /// selection reprices (option price or product surcharge moved) — the A18
    /// stop will fire.
    bool PriceChanged,
    /// The per-line drift REASONS from renormalising the stored selection
    /// through the SAME Spec 066 rules the box load path applies — option
    /// retired, group added/removed, selection-mode change — so the drawer can
    /// explain WHY a cart is blocked. Empty when nothing drifted.
    IReadOnlyList<Catalog.SelectionDrift> SelectionDrift,
    /// A classic BUNDLE line's nested component selections (Spec 042 carts):
    /// the line's own ProductVariantId is the bundle PRODUCT, so availability
    /// resolves through THESE — each component carries its own flag. Empty for
    /// non-bundle lines.
    IReadOnlyList<AdminCartLineComponentDto> Components);

/// <summary>One component selection inside a classic bundle cart line.</summary>
public record AdminCartLineComponentDto(
    Guid ProductVariantId,
    string Sku,
    string Name,
    decimal Quantity,
    /// Computed read-only, live editable carts only — same predicate as top-level lines.
    bool IsUnavailable);

public record AdminCartDetailDto(
    Guid CartId,
    string BuyerKind,
    Guid? BuyerPartyId,
    string Status,
    string Currency,
    AdminCartBoxMetaDto? BoxMeta,
    Guid? OrderId,
    DateTime UpdatedAtUtc,
    /// <summary>
    /// The cart's charged total, computed with the lines rather than derivable from them: a
    /// line carries only its price snapshot, while the charge adds the personalisation
    /// adjustment and unit surcharge, and a BoxDish snapshot is 0 because the box is priced
    /// as a container. A caller summing the lines would understate every personalised cart.
    /// </summary>
    decimal Total,
    IReadOnlyList<AdminCartLineDto> Lines);

public record AdminPartyActiveCartDto(Guid CartId, int Size, int Filled);

/// <summary>The Spec 081 Commerce-tab read: an arbitrary party's storefront
/// summary through the same party-scoped queries Spec 072 built for the
/// customer. <see cref="Adopted"/> is the RECORDED fact only — a party-bound
/// cart whose guest token was retired; no timestamps are invented.</summary>
public record AdminPartyStorefrontDto(
    IReadOnlyList<StorefrontOrderSummaryDto> Orders,
    AdminPartyActiveCartDto? ActiveCart,
    bool Adopted);
