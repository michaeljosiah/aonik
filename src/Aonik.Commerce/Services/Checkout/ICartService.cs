using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;

namespace Aonik.Commerce.Services.Checkout;

/// <summary>
/// Cart management for the Commerce module (Spec 042 §11/§12): create a cart, add simple or
/// build-your-own-box lines (price-snapshotted), and read it back. Returns DTOs.
/// </summary>
public interface ICartService
{
    Task<CartDto> CreateCartAsync(CreateCartCommand command, CancellationToken cancellationToken = default);
    Task<CartDto?> GetCartAsync(Guid cartId, CancellationToken cancellationToken = default);
    Task<CartDto> AddItemAsync(AddCartItemCommand command, CancellationToken cancellationToken = default);

    /// <summary>Adds a validated build-your-own-box selection as a single bundle line (§12).</summary>
    Task<CartDto> AddBundleAsync(AddBundleToCartCommand command, CancellationToken cancellationToken = default);

    Task<CartDto> RemoveItemAsync(Guid cartId, Guid cartItemId, CancellationToken cancellationToken = default);
}
