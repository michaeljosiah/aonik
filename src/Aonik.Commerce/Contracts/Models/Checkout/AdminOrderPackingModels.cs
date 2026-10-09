using System.Text.Json.Serialization;

using Aonik.Commerce.Services.Checkout;

namespace Aonik.Commerce.Contracts.Models.Checkout;

/// <summary>Recipient-facing packing facts. Purchaser contacts and payment handles are never included.</summary>
public record AdminOrderPackingDto(
    Guid OrderId,
    int? BoxSize,
    DateOnly? DeliveryDate,
    string? Timezone,
    DeliveryRecipientDto? Recipient,
    DeliveryAddressDto? Address,
    string? Notes,
    OrderGiftDto? Gift,
    IReadOnlyList<AdminOrderPackingItemDto> Items,
    IReadOnlyList<StorefrontOrderSelectionDto> Selections,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AdminOrderPackingPricesDto? Prices,
    string? OrderNumber = null);

public record AdminOrderPackingItemDto(int ItemIndex, string ItemType, string? Name, string? Sku, decimal? Quantity);

/// <summary>All monetary fields are inside this envelope, omitted entirely when the gift hides prices.</summary>
public record AdminOrderPackingPricesDto(AdminOrderChargeDto Charge, IReadOnlyList<AdminOrderPackingLinePriceDto> Items);

public record AdminOrderPackingLinePriceDto(int ItemIndex, decimal? UnitPrice, decimal Amount);
