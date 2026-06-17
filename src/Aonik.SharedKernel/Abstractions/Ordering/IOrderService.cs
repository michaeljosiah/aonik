namespace Aonik.SharedKernel.Abstractions.Ordering;

/// <summary>
/// The core, type-agnostic Order contract (Spec 041 / ADR-011). Owns the generic order spine —
/// create, read, list, status transitions, and funding/fulfilment linking — for every
/// <c>OrderType</c> (bill payment, remittance, product purchase, …). Domain modules
/// (<c>Aonik.Finance</c>, future <c>Aonik.Commerce</c>) compose this for persistence and lifecycle
/// and add only their type-specific orchestration (FX/compliance, inventory). Order, Payment, and
/// Ledger remain distinct: this never moves money — funding is a <c>PaymentIntent</c> linked via
/// <see cref="LinkFundingAsync"/>.
/// </summary>
public interface IOrderService
{
    Task<OrderDto> CreateAsync(CreateOrderCommand command, CancellationToken cancellationToken = default);

    Task<OrderDto?> GetAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<PagedResult<OrderSummary>> ListAsync(ListOrdersQuery query, CancellationToken cancellationToken = default);

    Task<OrderDto> TransitionAsync(Guid orderId, string toStatus, string? reason = null, CancellationToken cancellationToken = default);

    Task LinkFundingAsync(Guid orderId, Guid paymentIntentId, CancellationToken cancellationToken = default);

    Task LinkFulfilmentAsync(Guid orderId, OrderFulfilmentLink link, CancellationToken cancellationToken = default);
}
