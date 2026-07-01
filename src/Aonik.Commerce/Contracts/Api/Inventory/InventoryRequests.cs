namespace Aonik.Commerce.Contracts.Api.Inventory;

public record SetOnHandRequest(decimal OnHand);

public record InventoryAvailabilityResponse(Guid ProductVariantId, decimal Available);

/// <summary>Sets an ingredient level's reorder point + optional suggested reorder quantity
/// (Spec 052 §9). Null <paramref name="ReorderPoint"/> clears alerting for the item.</summary>
public record SetReorderPointRequest(decimal? ReorderPoint, decimal? ReorderQuantity = null);
