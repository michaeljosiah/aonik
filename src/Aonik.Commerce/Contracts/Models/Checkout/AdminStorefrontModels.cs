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

public record AdminCartBoxMetaDto(int Size, int Filled);

/// <summary>Carts admin list row (Spec 083 dependency callout 2). Box drift is
/// deliberately NOT on the list row — it is a computed, load-time state; the
/// detail read computes availability/price flags read-only instead.</summary>
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
    /// Computed read-only: cart-wide demand for the variant exceeds available
    /// stock right now. Nothing is persisted; the customer's next load runs
    /// the real Spec 068 repair.
    bool IsUnavailable,
    /// AddOn lines only — the current retail price no longer matches the
    /// snapshot (the A18 stop will fire at the customer's next continue).
    bool PriceChanged);

public record AdminCartDetailDto(
    Guid CartId,
    string BuyerKind,
    Guid? BuyerPartyId,
    string Status,
    string Currency,
    AdminCartBoxMetaDto? BoxMeta,
    Guid? OrderId,
    DateTime UpdatedAtUtc,
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
