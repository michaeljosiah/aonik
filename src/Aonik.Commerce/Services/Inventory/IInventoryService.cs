using Aonik.Commerce.Contracts.Models.Inventory;
using Aonik.Commerce.Entities.Inventory;

namespace Aonik.Commerce.Services.Inventory;

/// <summary>
/// Stock management + reservation for the Commerce module (Spec 042 §10, generalized by
/// Spec 052 §8). Reserve-before-order, commit-on-capture, release-on-cancel/expiry. One engine for
/// both stock-item kinds: a bundle checkout fans out over component variants; a production run
/// (Spec 056) will fan out over ingredients. The variant-keyed overloads are the original Spec 042
/// surface — thin wrappers over the stock-item-keyed core.
/// </summary>
public interface IInventoryService
{
    // ── Stock-item-keyed core (Spec 052 §8) ────────────────────────────────────────────────────

    /// <summary>Available units for a stock item at the default location (OnHand - Reserved).</summary>
    Task<decimal> GetAvailableAsync(StockItemRef item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Full level snapshot for a stock item at the default location (on-hand, reserved, available,
    /// reorder point/quantity). A never-stocked item reads back as zeros.
    /// </summary>
    Task<StockLevelDto> GetStockLevelAsync(StockItemRef item, CancellationToken cancellationToken = default);

    /// <summary>Sets the on-hand quantity for a stock item (admin stock adjustment).</summary>
    Task SetOnHandAsync(StockItemRef item, decimal onHand, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adjusts the on-hand quantity for a stock item by a signed delta — the receive path
    /// (Spec 054 §8: a goods receipt increments rather than overwrites, so a concurrent
    /// set/receive never loses the other's movement). Returns the resulting level snapshot.
    /// The caller owns sign/positivity validation.
    /// </summary>
    Task<StockLevelDto> AdjustOnHandAsync(StockItemRef item, decimal delta, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the reorder point (low-stock alert threshold on available stock) and the optional
    /// suggested reorder quantity for a stock item (Spec 052 §9). Null clears alerting.
    /// </summary>
    Task<StockLevelDto> SetReorderPointAsync(StockItemRef item, decimal? reorderPoint, decimal? reorderQuantity = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically reserves a set of (stock item, quantity) lines for a holder — all-or-nothing.
    /// Throws <see cref="InsufficientStockException"/> if any line cannot be satisfied; nothing is
    /// reserved. Lines may mix kinds (variants and ingredients) under one hold.
    /// </summary>
    Task ReserveAsync(Guid holdRef, IReadOnlyCollection<InventoryReservationLine> lines, CancellationToken cancellationToken = default);

    /// <summary>Commits all held reservations for a holder: draws down OnHand and clears Reserved.</summary>
    Task CommitAsync(Guid holdRef, CancellationToken cancellationToken = default);

    /// <summary>Releases all held reservations for a holder, freeing the stock.</summary>
    Task ReleaseAsync(Guid holdRef, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tenants that hold at least one expired reservation as of <paramref name="asOfUtc"/>. The Worker
    /// sweep narrows this list to the tenants whose Commerce module is enabled (Spec 097 §12.2) before
    /// calling <see cref="ReleaseExpiredAsync"/>.
    /// </summary>
    Task<IReadOnlyList<Guid>> FindTenantsWithExpiredReservationsAsync(DateTime? asOfUtc = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases every held reservation whose TTL has expired, regardless of stock-item kind. Returns the
    /// number released. When <paramref name="tenantIds"/> is given only those tenants are swept.
    /// </summary>
    Task<int> ReleaseExpiredAsync(DateTime? asOfUtc = null, IReadOnlyCollection<Guid>? tenantIds = null, CancellationToken cancellationToken = default);

    // ── Variant-keyed wrappers (the original Spec 042 surface; checkout/bundle callers unchanged) ──

    /// <summary>Available units for a variant at the default location (OnHand - Reserved).</summary>
    Task<decimal> GetAvailableAsync(Guid productVariantId, CancellationToken cancellationToken = default);

    /// <summary>Sets the on-hand quantity for a variant (admin stock adjustment).</summary>
    Task SetOnHandAsync(Guid productVariantId, decimal onHand, CancellationToken cancellationToken = default);
}

/// <summary>
/// A reference to the stock item a level or hold addresses — a product variant or an ingredient
/// (Spec 052 §8). <see cref="Kind"/> uses <see cref="StockItemKinds"/>.
/// </summary>
public readonly record struct StockItemRef(string Kind, Guid Id)
{
    public static StockItemRef Variant(Guid productVariantId) => new(StockItemKinds.ProductVariant, productVariantId);

    public static StockItemRef Ingredient(Guid ingredientId) => new(StockItemKinds.Ingredient, ingredientId);

    public bool IsIngredient => Kind == StockItemKinds.Ingredient;
}

/// <summary>One (stock item, quantity) line to reserve (Spec 042 §10, re-keyed by Spec 052 §8).</summary>
public record InventoryReservationLine(StockItemRef Item, decimal Quantity)
{
    /// <summary>Variant-keyed convenience — the original Spec 042 checkout shape.</summary>
    public InventoryReservationLine(Guid productVariantId, decimal quantity)
        : this(StockItemRef.Variant(productVariantId), quantity)
    {
    }
}

/// <summary>Thrown when a reservation cannot be satisfied from available stock.</summary>
public sealed class InsufficientStockException : Exception
{
    public string StockItemKind { get; }
    public Guid StockItemId { get; }
    public decimal Requested { get; }
    public decimal Available { get; }

    public InsufficientStockException(StockItemRef item, decimal requested, decimal available)
        : base($"Insufficient stock for {(item.IsIngredient ? "ingredient" : "variant")} '{item.Id}': requested {requested}, available {available}.")
    {
        StockItemKind = item.Kind;
        StockItemId = item.Id;
        Requested = requested;
        Available = available;
    }
}
