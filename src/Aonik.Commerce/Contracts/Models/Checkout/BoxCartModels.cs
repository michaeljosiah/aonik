using System.Text.Json;

namespace Aonik.Commerce.Contracts.Models.Checkout;

// ─── Cart access (Spec 068 R10) ─────────────────────────────────────────────

/// <summary>
/// Who is asking. Cart ids travel in URLs and URLs leak — possession of an id is NOT access.
/// Guest carts require the server-minted token (disclosed exactly once, at create); party-bound
/// carts require an authenticated principal matching BuyerPartyId instead. Every direct consumer
/// of the cart services — endpoints, checkout, agent tools, anything future — presents one of
/// these; authorization lives in one shared authorizer at the service boundary, and any mismatch
/// is a 404 indistinguishable from an unknown cart.
/// </summary>
public sealed record CartAccessContext(string? GuestToken, Guid? AuthenticatedPartyId)
{
    public static CartAccessContext ForGuest(string? token) => new(token, null);

    public static CartAccessContext ForParty(Guid partyId) => new(null, partyId);
}

// ─── The §7 payload ─────────────────────────────────────────────────────────

public record BoxLineDto(
    Guid LineId,
    Guid ProductId,
    Guid VariantId,
    string Name,
    int Quantity,
    /// The exact canonical selection (Spec 066 §7) — an object, never a string.
    JsonElement? Personalisation,
    string PersonalisationSummary,
    bool IsDefaultPersonalisation,
    /// Per unit, signed.
    decimal PersonalisationAdjustment,
    decimal UnitSurcharge,
    /// Empty for AddOn lines — add-ons fill no slot (Spec 071).
    Guid SlotId,
    /// A13 — flagged (never silently removed); adds/increases reject, continue/checkout block.
    bool IsUnavailable,
    /// BoxDish or AddOn (Spec 071) — additive; pre-071 clients never see AddOn rows.
    string LineKind = "BoxDish",
    /// AddOn lines only: the retail unit price (the deliberate no-price-rule exception).
    decimal? UnitPrice = null);

public record BoxDto(
    Guid CartId,
    Guid BundleProductId,
    int Size,
    string Currency,
    IReadOnlyList<BoxLineDto> Lines);

/// <summary>One ordered, named, additive component. Clients render by iterating — never
/// reconstruct the total from known keys, so a future "addOns" component is a non-event.</summary>
public record QuoteComponentDto(string Key, decimal Amount);

public record BoxQuoteDto(
    IReadOnlyList<QuoteComponentDto> Components,
    /// Struck-through display value; NOT a component.
    decimal DeliveryList,
    /// Σ components — never a hard-coded formula (A24).
    decimal Total,
    string Currency,
    /// BoxDish units only (§4.1) — an add-on never changes these three.
    int UnitsSelected,
    int BoxSize,
    int SpacesLeft,
    bool IsFull);

/// <summary>A customer-visible catalogue-drift notice (§8) — remaps, drops, merges, unavailable
/// flags. The storefront tells the customer what changed and why.</summary>
public record BoxChangeDto(
    Guid? LineId,
    string? Group,
    string? From,
    string? To,
    string Reason,
    decimal? PriceDelta = null,
    Guid? MergedIntoLineId = null);

/// <summary>Known <see cref="BoxChangeDto.Reason"/> values beyond the Spec 066 drift reasons.</summary>
public static class BoxChangeReasons
{
    /// <summary>Product/variant no longer purchasable or demand exceeds availability (A13).</summary>
    public const string Unavailable = "unavailable";

    /// <summary>A remapped selection now equals another line's; the quantities merged.</summary>
    public const string LineMerged = "line-merged";
}

/// <summary>Every read and write returns the whole box + the authoritative quote, so concurrent
/// tabs self-correct on their next action. CartToken is populated ONLY by create (R10).</summary>
public record BoxCartDto(
    BoxDto Box,
    BoxQuoteDto Quote,
    IReadOnlyList<BoxChangeDto> Changes,
    string? CartToken = null);

// ─── Commands (§10) ─────────────────────────────────────────────────────────

public record AddBoxLineCommand(
    Guid ProductVariantId,
    int Quantity,
    JsonElement? Personalisation = null,
    /// Named, or auto-resolved when exactly one slot is eligible — ambiguous otherwise (R5).
    Guid? BundleSlotId = null);

public record CreateBoxCartCommand(
    Guid BundleProductId,
    int Size,
    /// The dish-detail → Step 1 handoff: the viewed dish arrives already in the box.
    AddBoxLineCommand? FirstLine = null,
    Guid? BuyerPartyId = null);

/// <summary>Spec 071 — add an ordinary retail product alongside the box. No slot, no capacity;
/// the retail price in the cart currency is mandatory.</summary>
public record AddBoxExtraCommand(
    Guid ProductVariantId,
    int Quantity,
    System.Text.Json.JsonElement? Personalisation = null);

/// <summary>Omitted members mean unchanged. Quantity 0 deletes the line; ApplyToUnits (1 ≤ n ≤
/// line quantity) applies a personalisation change to n units only — split semantics, atomic.</summary>
public record UpdateBoxLineCommand(
    int? Quantity = null,
    JsonElement? Personalisation = null,
    int? ApplyToUnits = null);
