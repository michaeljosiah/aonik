using Aonik.Commerce.Contracts.Models.Checkout;

namespace Aonik.Commerce.Services.Checkout;

public interface IOrderReorderService
{
    /// <summary>Create a fresh box from the authenticated party's paid dishes, at current prices.
    /// Null means the source order is not owned by this party in the current tenant.</summary>
    Task<BoxCartDto?> ReorderAsync(Guid orderId, Guid partyId, CancellationToken cancellationToken = default);
}
