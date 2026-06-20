namespace Aonik.SharedKernel.Abstractions.Ordering;

/// <summary>
/// Reads order history for cross-module consumers (notably PersonalFinance).
/// PersonalFinance must not reference <c>Aonik.Finance.Entities.Orders</c> directly;
/// it consumes order history through this read contract instead.
///
/// The shape returned (<see cref="OrderHistoryItem"/>) carries only what
/// PersonalFinance actually consumes — it is not a projection of the full Order entity.
/// See <a href="../../docs/specifications/027.extract-personal-finance-module.html">Spec 027</a>.
/// </summary>
public interface ICustomerOrderHistoryReader
{
    /// <summary>
    /// Returns all orders for a party (resolved via the OrderPartyRoles join)
    /// inside the given UTC window, ordered by most recent first.
    /// </summary>
    Task<IReadOnlyList<OrderHistoryItem>> GetForPartyAsync(
        Guid tenantId,
        Guid partyId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns orders matching the supplied identifiers, scoped to the tenant.
    /// Used by the FinancialLifeGraph loader to hydrate bill-linked orders.
    /// </summary>
    Task<IReadOnlyList<OrderHistoryItem>> GetByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent orders where the given party is the payer, along
    /// with each order's full party-role mapping so callers can resolve
    /// beneficiary parties without taking a dependency on
    /// <c>Aonik.Finance.Entities.Orders.OrderPartyRole</c>.
    /// </summary>
    Task<IReadOnlyList<OrderWithPartyRolesItem>> GetRecentForPayerAsync(
        Guid tenantId,
        Guid payerPartyId,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether an order exists in the given tenant. Used by graph node-type
    /// resolution where only existence (not the full record) matters.
    /// </summary>
    Task<bool> ExistsAsync(
        Guid tenantId,
        Guid orderId,
        CancellationToken cancellationToken = default);
}
