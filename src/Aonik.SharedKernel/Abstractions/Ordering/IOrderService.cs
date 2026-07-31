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
    /// Same filters and paging as <see cref="ListAsync"/>, but each order is returned as a full
    /// <see cref="OrderDto"/> including its line items. Additive (Spec 055 §9): a consumer that
    /// aggregates per-line retail fields (<c>Quantity</c>, <c>ProductId</c> — the production
    /// sheet) needs the lines, and <see cref="OrderSummary"/> deliberately carries only a count.
    /// </summary>
    Task<PagedResult<OrderDto>> ListWithItemsAsync(ListOrdersQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Per-party order counts and value, aggregated in the database for a whole SET of payers in
    /// one query — what the Spec 080 customers registry renders for a page of customers, where
    /// listing each party's orders in turn would be N+1.
    /// </summary>
    /// <remarks>
    /// SPINE-WIDE by design (ADR-011): every <c>OrderType</c> counts — box purchases, bill
    /// payments, transfers alike — because there is one order spine and a registry that counted
    /// only one product line would understate the customer. Payer-scoped in v1, matching
    /// <see cref="ListOrdersQuery.PayerPartyId"/>, so registry and customer detail agree by
    /// construction; participant-role expansion is a joint follow-up for both.
    /// Every requested id is present in the result; a party with no orders maps to an empty
    /// aggregate rather than being absent.
    /// </remarks>
    Task<IReadOnlyDictionary<Guid, PartyOrderAggregate>> GetPartyOrderAggregatesAsync(
        IReadOnlyCollection<Guid> payerPartyIds,
        CancellationToken cancellationToken = default);

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
