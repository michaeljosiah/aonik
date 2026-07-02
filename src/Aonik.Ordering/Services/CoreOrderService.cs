using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

using Aonik.Finance.Entities.Orders;
using Aonik.Ordering.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;

namespace Aonik.Ordering.Services;

/// <summary>
/// Spec 041 / ADR-011 Phase 3 — the type-agnostic implementation of the core
/// <see cref="IOrderService"/> contract, now resident in <c>Aonik.Ordering</c> over the
/// module-scoped <see cref="OrderingDbContext"/>. Owns the generic order spine (create, read,
/// list, transitions, funding/fulfilment links) for every <c>OrderType</c>, including
/// <c>ProductPurchase</c>. Type-specific creation (bill payment, remittance) lives in
/// <c>Aonik.Finance</c> and composes this contract.
/// </summary>
internal sealed class CoreOrderService : IOrderService
{
    private readonly OrderingDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;

    public CoreOrderService(
        OrderingDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock,
        ICurrentUserProvider currentUserProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<OrderDto> CreateAsync(CreateOrderCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Items is null || command.Items.Count == 0)
        {
            throw new ArgumentException("An order requires at least one line item.", nameof(command));
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Normalize once and use the same value for both the lookup and the insert: a blank key is
        // stored as NULL (exempt from the filtered unique index), and trimming makes a
        // whitespace-padded retry hit the existing order instead of creating a duplicate.
        var idempotencyKey = string.IsNullOrWhiteSpace(command.IdempotencyKey)
            ? null
            : command.IdempotencyKey.Trim();

        if (idempotencyKey is not null)
        {
            var existing = await _dbContext.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(
                    o => o.OrderType == command.OrderType && o.IdempotencyKey == idempotencyKey,
                    cancellationToken);
            if (existing is not null)
            {
                return MapToDto(existing);
            }
        }

        var orderId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            TenantId = tenantId,
            OrderType = command.OrderType,
            Status = OrderStatuses.Draft,
            IdempotencyKey = idempotencyKey,
            PayerPartyId = command.PayerPartyId,
            CurrencyIn = command.CurrencyIn,
            AmountIn = command.AmountIn ?? command.Items.Sum(i => i.AmountIn),
            ProvenanceJson = command.ProvenanceJson ?? string.Empty
        };

        foreach (var item in command.Items)
        {
            order.Items.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OrderId = orderId,
                ItemType = item.ItemType,
                ItemIndex = item.ItemIndex,
                Status = "Valid",
                DetailsJson = item.DetailsJson ?? "{}",
                ReceiverPartyId = item.ReceiverPartyId,
                AmountIn = item.AmountIn,
                CurrencyIn = item.CurrencyIn,
                CurrencyOut = item.CurrencyIn,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                ProductId = item.ProductId,
                Sku = item.Sku
            });

            if (item.ReceiverPartyId is { } receiverPartyId)
            {
                order.PartyRoles.Add(new OrderPartyRole
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    OrderId = orderId,
                    PartyId = receiverPartyId,
                    Role = OrderPartyRoles.Receiver
                });
            }
        }

        if (command.PayerPartyId is { } payerPartyId)
        {
            order.PartyRoles.Add(new OrderPartyRole
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OrderId = orderId,
                PartyId = payerPartyId,
                Role = OrderPartyRoles.Payer
            });
        }

        // Spec 053 §10 — explicitly supplied party roles (e.g. the Supplier counterparty on a
        // purchase order), materialized alongside the auto-materialized Payer/Receiver roles.
        // Entries duplicating an already-added (party, role) pair — auto-materialized or an
        // earlier supplied entry — are deduped so one role never lands twice on the same order.
        if (command.PartyRoles is { Count: > 0 })
        {
            foreach (var partyRole in command.PartyRoles)
            {
                if (partyRole.PartyId == Guid.Empty)
                {
                    throw new ArgumentException("A supplied party role requires a non-empty PartyId.", nameof(command));
                }
                if (string.IsNullOrWhiteSpace(partyRole.Role))
                {
                    throw new ArgumentException("A supplied party role requires a non-empty Role.", nameof(command));
                }

                var role = partyRole.Role.Trim();
                if (order.PartyRoles.Any(r =>
                        r.PartyId == partyRole.PartyId && string.Equals(r.Role, role, StringComparison.Ordinal)))
                {
                    continue;
                }

                order.PartyRoles.Add(new OrderPartyRole
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    OrderId = orderId,
                    PartyId = partyRole.PartyId,
                    Role = role
                });
            }
        }

        order.HistoryEvents.Add(BuildHistoryEvent(tenantId, orderId, "Created", string.Empty));

        _dbContext.Orders.Add(order);

        // Capture the outbox row we enqueue so it can be detached on a race loss — IIntegrationEvent
        // .EventId regenerates per access, so it can't be matched after the fact.
        var outboxBefore = _dbContext.ChangeTracker.Entries<OutboxMessage>().Select(e => e.Entity).ToHashSet();
        _dbContext.EnqueueIntegrationEvent(new OrderCreatedEvent(
            tenantId, orderId, order.OrderType, order.PayerPartyId, order.AmountIn, order.CurrencyIn));
        var enqueuedOutbox = _dbContext.ChangeTracker.Entries<OutboxMessage>()
            .Where(e => !outboxBefore.Contains(e.Entity))
            .ToList();

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (idempotencyKey is not null)
        {
            // Lost an idempotency race: a concurrent create with the same
            // (TenantId, OrderType, IdempotencyKey) committed first and tripped the filtered unique
            // index. Detach our rejected graph (and its orphaned outbox event) and return the winner,
            // so concurrent idempotent requests still receive a single coherent order.
            DetachOrderGraph(order, enqueuedOutbox);

            var winner = await _dbContext.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(
                    o => o.OrderType == command.OrderType && o.IdempotencyKey == idempotencyKey,
                    cancellationToken);
            if (winner is null)
            {
                throw;
            }

            return MapToDto(winner);
        }

        return MapToDto(order);
    }

    private void DetachOrderGraph(Order order, IEnumerable<EntityEntry<OutboxMessage>> enqueuedOutbox)
    {
        // The whole graph was cascade-tracked as Added by _dbContext.Orders.Add. After a failed
        // insert we detach every node so the rejected order can't be replayed by a later
        // SaveChanges on this scoped context.
        foreach (var historyEvent in order.HistoryEvents)
        {
            _dbContext.Entry(historyEvent).State = EntityState.Detached;
        }

        foreach (var partyRole in order.PartyRoles)
        {
            _dbContext.Entry(partyRole).State = EntityState.Detached;
        }

        foreach (var item in order.Items)
        {
            _dbContext.Entry(item).State = EntityState.Detached;
        }

        _dbContext.Entry(order).State = EntityState.Detached;

        // Detach the orphaned outbox event so OrderCreatedEvent is never dispatched for the rejected
        // order (the winning create enqueued its own).
        foreach (var outbox in enqueuedOutbox)
        {
            outbox.State = EntityState.Detached;
        }
    }

    public async Task<OrderDto?> GetAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        return order is null ? null : MapToDto(order);
    }

    public async Task<OrderDto?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        // Same normalization as CreateAsync's dedupe, so a whitespace-padded retry key still
        // resolves to the order the original create stored (trimmed). Tenant scoping comes from
        // the global query filter, exactly like the CreateAsync lookup.
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return null;
        }
        var key = idempotencyKey.Trim();

        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.IdempotencyKey == key, cancellationToken);

        return order is null ? null : MapToDto(order);
    }

    public async Task<PagedResult<OrderSummary>> ListAsync(ListOrdersQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var (pageNumber, pageSize) = NormalizePaging(query);

        var orders = ApplyListFilters(_dbContext.Orders.AsNoTracking(), query);

        var totalCount = await orders.CountAsync(cancellationToken);

        var items = await orders
            .OrderByDescending(o => o.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderSummary(
                o.Id, o.OrderType, o.Status, o.AmountIn, o.CurrencyIn, o.CreatedAt, o.Items.Count))
            .ToListAsync(cancellationToken);

        return new PagedResult<OrderSummary>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<PagedResult<OrderDto>> ListWithItemsAsync(ListOrdersQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var (pageNumber, pageSize) = NormalizePaging(query);

        // Same filters as ListAsync (one predicate, Spec 055 §9's centralisation), but the page is
        // materialised with line items so per-line retail fields can be aggregated.
        var orders = ApplyListFilters(_dbContext.Orders.AsNoTracking(), query);

        var totalCount = await orders.CountAsync(cancellationToken);

        var page = await orders
            .OrderByDescending(o => o.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Include(o => o.Items)
            .ToListAsync(cancellationToken);

        return new PagedResult<OrderDto>(page.Select(MapToDto).ToList(), totalCount, pageNumber, pageSize);
    }

    private static (int PageNumber, int PageSize) NormalizePaging(ListOrdersQuery query)
        => (query.PageNumber < 1 ? 1 : query.PageNumber,
            query.PageSize is < 1 or > 200 ? 20 : query.PageSize);

    /// <summary>The one list predicate <see cref="ListAsync"/> and <see cref="ListWithItemsAsync"/>
    /// share. The created-range bounds are half-open ([From, To) — from inclusive, to exclusive,
    /// Spec 055 §9) so adjacent windows never double-count a boundary order.</summary>
    private static IQueryable<Order> ApplyListFilters(IQueryable<Order> orders, ListOrdersQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.OrderType))
        {
            orders = orders.Where(o => o.OrderType == query.OrderType);
        }
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            orders = orders.Where(o => o.Status == query.Status);
        }
        if (query.PayerPartyId is { } payerPartyId)
        {
            orders = orders.Where(o => o.PayerPartyId == payerPartyId);
        }
        if (query.CreatedFromUtc is { } createdFromUtc)
        {
            orders = orders.Where(o => o.CreatedAt >= createdFromUtc);
        }
        if (query.CreatedToUtc is { } createdToUtc)
        {
            orders = orders.Where(o => o.CreatedAt < createdToUtc);
        }

        return orders;
    }

    public async Task<OrderDto> TransitionAsync(Guid orderId, string toStatus, string? reason = null, string? expectedFromStatus = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toStatus))
        {
            throw new ArgumentException("A target status is required.", nameof(toStatus));
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Order {orderId} was not found.");

        // Compare-and-set (Spec 053 §13): the caller's guard read a snapshot; without this check an
        // interleaved transition could invalidate it between the read and this write (e.g. a
        // cancelled PO resurrected to Pending by a stale submit). The expectation is verified on
        // THIS tracked read; the RowVersion concurrency token still guards the write itself. The
        // spine remains state-machine-free — it only honours an expectation the caller sends.
        if (expectedFromStatus is not null && !string.Equals(order.Status, expectedFromStatus, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Order {orderId} is {order.Status}, not the expected {expectedFromStatus}; " +
                $"the transition to {toStatus} was not applied.");
        }

        var previousStatus = order.Status;
        if (!string.Equals(previousStatus, toStatus, StringComparison.Ordinal))
        {
            order.Status = toStatus;
            _dbContext.OrderHistoryEvents.Add(
                BuildHistoryEvent(tenantId, orderId, "StatusChanged", reason ?? string.Empty));
            _dbContext.EnqueueIntegrationEvent(
                new OrderStatusChangedEvent(tenantId, orderId, previousStatus, toStatus));
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return MapToDto(order);
    }

    public async Task LinkFundingAsync(Guid orderId, Guid paymentIntentId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        await EnsureOrderExistsAsync(orderId, cancellationToken);

        _dbContext.OrderFundingRefs.Add(new OrderFundingRef
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = orderId,
            PaymentIntentId = paymentIntentId
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task LinkFulfilmentAsync(Guid orderId, OrderFulfilmentLink link, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(link);
        var set = new[] { link.PayoutId, link.PaymentIntentId, link.PartnerBillPaymentId }.Count(id => id is not null);
        if (set != 1)
        {
            throw new ArgumentException("Exactly one fulfilment reference must be set.", nameof(link));
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        await EnsureOrderExistsAsync(orderId, cancellationToken);

        _dbContext.OrderFulfilmentRefs.Add(new OrderFulfilmentRef
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = orderId,
            PayoutId = link.PayoutId,
            PaymentIntentId = link.PaymentIntentId,
            PartnerBillPaymentId = link.PartnerBillPaymentId
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureOrderExistsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.Orders.AnyAsync(o => o.Id == orderId, cancellationToken);
        if (!exists)
        {
            throw new KeyNotFoundException($"Order {orderId} was not found.");
        }
    }

    private OrderHistoryEvent BuildHistoryEvent(Guid tenantId, Guid orderId, string eventType, string detailsJson)
    {
        var userId = _currentUserProvider.GetCurrentUserId();
        return new OrderHistoryEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = orderId,
            EventType = eventType,
            EventAt = _clock.UtcNow,
            ActorType = userId is null ? "System" : "User",
            ActorId = userId ?? Guid.Empty,
            DetailsJson = detailsJson
        };
    }

    private static OrderDto MapToDto(Order order)
        => new(
            order.Id,
            order.TenantId,
            order.OrderType,
            order.Status,
            order.PayerPartyId,
            order.AmountIn,
            order.CurrencyIn,
            order.CreatedAt,
            order.Items
                .OrderBy(i => i.ItemIndex)
                .Select(i => new OrderItemDto(
                    i.Id, i.ItemType, i.ItemIndex, i.Status, i.AmountIn, i.CurrencyIn,
                    i.ReceiverPartyId, i.Quantity, i.UnitPrice, i.ProductId, i.Sku, i.DetailsJson))
                .ToList());
}
