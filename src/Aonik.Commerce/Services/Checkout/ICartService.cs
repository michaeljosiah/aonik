using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;

namespace Aonik.Commerce.Services.Checkout;

/// <summary>
/// Cart management for the Commerce module (Spec 042 §11/§12): create a cart, add simple or
/// build-your-own-box lines (price-snapshotted), and read it back. Returns DTOs.
/// </summary>
public interface ICartService
{
    /// <summary>Create a cart. The guest token is server-minted and disclosed ONLY in this
    /// response (R10); any client-supplied token value is ignored.</summary>
    Task<CartDto> CreateCartAsync(CreateCartCommand command, CancellationToken cancellationToken = default);
    Task<CartDto?> GetCartAsync(Guid cartId, CartAccessContext access, CancellationToken cancellationToken = default);
    Task<CartDto> AddItemAsync(AddCartItemCommand command, CartAccessContext access, CancellationToken cancellationToken = default);

    /// <summary>Adds a validated build-your-own-box selection as a single bundle line (§12).</summary>
    Task<CartDto> AddBundleAsync(AddBundleToCartCommand command, CartAccessContext access, CancellationToken cancellationToken = default);

    Task<CartDto> RemoveItemAsync(Guid cartId, Guid cartItemId, CartAccessContext access, CancellationToken cancellationToken = default);

    /// <summary>Spec 072 Y4 — guest→account adoption: the guest token proves possession, the
    /// principal supplies the party. Sets BuyerPartyId, clears the token (Z3). Idempotent when
    /// the SAME party adopts again; every other mismatch is the R10 404 (Z2). Open, order-less
    /// carts only (Z4).</summary>
    Task<CartDto> AdoptAsync(Guid cartId, Guid partyId, CartAccessContext access, CancellationToken cancellationToken = default);
}
