namespace Aonik.Commerce.Services.Inventory;

/// <summary>
/// Stock management + reservation for the Commerce module (Spec 042 §10). Reserve-before-order,
/// commit-on-capture, release-on-cancel/expiry. A bundle holds no stock of its own — reservation
/// and commit fan out over the chosen component variants.
/// </summary>
public interface IInventoryService
{
    /// <summary>Available units for a variant at the default location (OnHand - Reserved).</summary>
    Task<decimal> GetAvailableAsync(Guid productVariantId, CancellationToken cancellationToken = default);

    /// <summary>Sets the on-hand quantity for a variant (admin stock adjustment).</summary>
    Task SetOnHandAsync(Guid productVariantId, decimal onHand, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically reserves a set of (variant, quantity) lines for a cart — all-or-nothing. Throws
    /// <see cref="InsufficientStockException"/> if any line cannot be satisfied; nothing is reserved.
    /// </summary>
    Task ReserveAsync(Guid cartId, IReadOnlyCollection<InventoryReservationLine> lines, CancellationToken cancellationToken = default);

    /// <summary>Commits all held reservations for a cart: draws down OnHand and clears Reserved.</summary>
    Task CommitAsync(Guid cartId, CancellationToken cancellationToken = default);

    /// <summary>Releases all held reservations for a cart, freeing the stock.</summary>
    Task ReleaseAsync(Guid cartId, CancellationToken cancellationToken = default);

    /// <summary>Releases every held reservation whose TTL has expired. Returns the number released.</summary>
    Task<int> ReleaseExpiredAsync(DateTime? asOfUtc = null, CancellationToken cancellationToken = default);
}

/// <summary>One (variant, quantity) line to reserve (Spec 042 §10).</summary>
public record InventoryReservationLine(Guid ProductVariantId, decimal Quantity);

/// <summary>Thrown when a reservation cannot be satisfied from available stock.</summary>
public sealed class InsufficientStockException : Exception
{
    public Guid ProductVariantId { get; }
    public decimal Requested { get; }
    public decimal Available { get; }

    public InsufficientStockException(Guid productVariantId, decimal requested, decimal available)
        : base($"Insufficient stock for variant '{productVariantId}': requested {requested}, available {available}.")
    {
        ProductVariantId = productVariantId;
        Requested = requested;
        Available = available;
    }
}
