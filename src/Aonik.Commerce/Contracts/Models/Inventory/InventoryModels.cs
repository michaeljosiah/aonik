namespace Aonik.Commerce.Contracts.Models.Inventory;

/// <summary>
/// Snapshot of one stock level (Spec 052 §8/§9). <paramref name="StockItemId"/> is the variant or
/// ingredient id per <paramref name="StockItemKind"/>; Available = OnHand - Reserved. A stock item
/// that has never been stocked reads back as zeros with no reorder point.
/// </summary>
public record StockLevelDto(
    string StockItemKind,
    Guid StockItemId,
    decimal OnHand,
    decimal Reserved,
    decimal Available,
    decimal? ReorderPoint,
    decimal? ReorderQuantity);
