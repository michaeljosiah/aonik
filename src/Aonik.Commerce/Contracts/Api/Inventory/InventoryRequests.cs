namespace Aonik.Commerce.Contracts.Api.Inventory;

public record SetOnHandRequest(decimal OnHand);

public record InventoryAvailabilityResponse(Guid ProductVariantId, decimal Available);
