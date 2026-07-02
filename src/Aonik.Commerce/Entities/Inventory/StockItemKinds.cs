namespace Aonik.Commerce.Entities.Inventory;

/// <summary>
/// Known values for the stock-item discriminator on <see cref="InventoryLevel"/> and
/// <see cref="InventoryReservation"/> (Spec 052 §7/§8). An open string on the entities so new
/// kinds are additive (mirroring how <c>OrderType</c> is modelled); this is the known-values helper.
/// </summary>
public static class StockItemKinds
{
    public const string ProductVariant = "ProductVariant";
    public const string Ingredient = "Ingredient";
}
