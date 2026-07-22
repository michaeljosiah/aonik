using System.Text.Json;

namespace Aonik.Commerce.Contracts.Api.Checkout;

public record AddBoxLineRequest(
    Guid ProductVariantId,
    int Quantity,
    /// The personalisation selection (Spec 066 shape); omitted means all defaults.
    JsonElement? Personalisation = null,
    /// Named, or auto-resolved when exactly one slot is eligible (R5).
    Guid? BundleSlotId = null);

public record CreateBoxCartRequest(
    Guid BundleProductId,
    int Size,
    /// The dish-detail handoff: the viewed dish arrives already in the box.
    AddBoxLineRequest? FirstLine = null,
    Guid? BuyerPartyId = null);

public record ChangeBoxSizeRequest(int Size);

/// <summary>Omitted members mean unchanged; Quantity 0 deletes the line; ApplyToUnits applies a
/// personalisation change to n units (split semantics, atomic).</summary>
public record UpdateBoxLineRequest(
    int? Quantity = null,
    JsonElement? Personalisation = null,
    int? ApplyToUnits = null);
