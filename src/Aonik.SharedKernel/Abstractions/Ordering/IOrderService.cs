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

    /// <summary>
    /// The order previously created with <paramref name="idempotencyKey"/> (tenant-scoped, matched
    /// on the same normalized column <see cref="CreateAsync"/> dedupes on), or null. Additive
    /// (Spec 053 §12): a type-specific creation flow whose validation consults state the first
    /// attempt itself mutated (e.g. a shortfall seed flipping its source alerts) resolves a
    /// lost-response retry through this lookup FIRST — <see cref="CreateAsync"/>'s internal dedupe
    /// only helps callers that reach it.
    /// </summary>
    Task<OrderDto?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    Task<PagedResult<OrderSummary>> ListAsync(ListOrdersQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a status transition (history + <c>OrderStatusChangedEvent</c>); a same-status call
    /// is a no-op. The spine enforces no state machine — but a caller that guards its own machine
    /// can send its observed status in <paramref name="expectedFromStatus"/> (Spec 053 §13): when
    /// set and the order's current status differs on the tracked read, the transition throws
    /// instead of applying, closing the caller's check-then-act window (compare-and-set — the
    /// EXPECTATION travels with the call; the spine itself stays state-machine-free).
    /// </summary>
    Task<OrderDto> TransitionAsync(Guid orderId, string toStatus, string? reason = null, string? expectedFromStatus = null, CancellationToken cancellationToken = default);

    Task LinkFundingAsync(Guid orderId, Guid paymentIntentId, CancellationToken cancellationToken = default);

    Task LinkFulfilmentAsync(Guid orderId, OrderFulfilmentLink link, CancellationToken cancellationToken = default);
}
