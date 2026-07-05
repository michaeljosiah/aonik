using Aonik.SharedKernel.Abstractions;

namespace Aonik.SharedKernel.Abstractions.Ordering;

/// <summary>
/// Customer-facing, party-scoped projection over a customer's own payment orders (bill
/// payments, transfers, remittances) — the shape a personal-finance assistant surfaces to the
/// order's owner. Distinct from the type-agnostic <see cref="IOrderService"/> spine (Spec 041 /
/// ADR-011): that owns generic create/list/transition for every <c>OrderType</c> and returns the
/// lean spine DTOs; this returns the remittance-rich customer shape (destination currency, amount
/// out, fees, receiver / biller names) that the spine deliberately omits, and every method is
/// scoped to a single owner <paramref name="partyId"/> so a caller can never read or cancel
/// another party's order.
///
/// Lives in SharedKernel so PersonalFinance (the "Simi" agent tools) can consume it without a
/// project reference on <c>Aonik.Finance</c>. The implementation lives in the module that owns the
/// Order persistence (Aonik.Finance) and is tenant-scoped through that module's
/// <c>ITenantProvider</c>, exactly as <see cref="IOrderService"/> is.
/// See <a href="../../../../docs/specifications/027.extract-personal-finance-module.html">Spec 027</a>.
/// </summary>
public interface ICustomerOrderService
{
    /// <summary>
    /// Lists the party's own orders (as payer), most recent first, filtered by optional
    /// <paramref name="status"/> / <paramref name="orderType"/> and paged. Never returns orders
    /// belonging to another party.
    /// </summary>
    Task<PagedResult<CustomerOrderSummary>> ListForPartyAsync(
        Guid partyId,
        string? status,
        string? orderType,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the rich detail of a single order the party owns, or <c>null</c> when the order
    /// does not exist or is owned by a different party (ownership is enforced server-side, so the
    /// caller cannot leak another party's order by guessing an id).
    /// </summary>
    Task<CustomerOrderDetail?> GetForPartyAsync(
        Guid partyId,
        Guid orderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an unsettled order the party owns and returns its resulting detail. Already-terminal
    /// orders (Cancelled / Completed / Failed) are a no-op that returns the current detail. Throws
    /// when the order does not exist or is owned by a different party.
    /// </summary>
    Task<CustomerOrderDetail> CancelForPartyAsync(
        Guid partyId,
        Guid orderId,
        string? reason,
        CancellationToken cancellationToken = default);
}
