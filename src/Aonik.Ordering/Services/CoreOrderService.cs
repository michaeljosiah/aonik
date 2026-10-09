using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
    private readonly IOrderNumberGenerator _orderNumbers;

    public CoreOrderService(
        OrderingDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock,
        ICurrentUserProvider currentUserProvider,
        IOrderNumberGenerator orderNumbers)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
        _orderNumbers = orderNumbers;
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

        ValidateNames(command.Items);

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
                Sku = item.Sku,
                NameSnapshot = item.NameSnapshot
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

        order.OrderNumber = await _orderNumbers.GenerateAsync(cancellationToken);

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

    public async Task<OrderDto> RefreshPendingItemsAsync(Guid orderId, Guid revisionId, Guid? payerPartyId, string currency,
        IReadOnlyList<OrderItemCommand> items, CancellationToken cancellationToken = default)
    {
        if (revisionId == Guid.Empty || items.Count == 0) throw new ArgumentException("A checkout revision and items are required.");
        ValidateNames(items);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        foreach (var entry in _dbContext.ChangeTracker.Entries().Where(entry =>
                       entry.Entity is Order order && order.Id == orderId && order.TenantId == tenantId
                       || entry.Entity is OrderItem item && item.OrderId == orderId && item.TenantId == tenantId
                       || entry.Entity is OrderPartyRole partyRole && partyRole.OrderId == orderId && partyRole.TenantId == tenantId
                       || entry.Entity is OrderHistoryEvent history && history.OrderId == orderId && history.TenantId == tenantId).ToList())
            entry.State = EntityState.Detached;
        var current = await _dbContext.Orders.Include(order => order.Items).Include(order => order.PartyRoles)
            .SingleOrDefaultAsync(order => order.Id == orderId && order.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Order was not found.");
        if (current.OrderType != OrderTypeCodes.ProductPurchase
            || current.Status is not (OrderStatusCodes.Draft or "PendingFunding"))
            throw new InvalidStateException("Only an unpaid product purchase can be refreshed.");
        var revision = JsonSerializer.Serialize(new { commerceCheckoutAttemptId = revisionId });
        if (current.ProvenanceJson == revision) return MapToDto(current);
        if (!string.IsNullOrWhiteSpace(current.ProvenanceJson)
            && !current.ProvenanceJson.Contains("\"commerceCheckoutAttemptId\"", StringComparison.Ordinal))
            throw new InvalidStateException("This order was not prepared by Commerce checkout.");
        _dbContext.OrderItems.RemoveRange(current.Items);
        current.Items = items.Select(item => new OrderItem
        {
            Id = Guid.NewGuid(), TenantId = tenantId, OrderId = orderId,
            ItemType = item.ItemType, ItemIndex = item.ItemIndex, Status = "Valid",
            DetailsJson = item.DetailsJson ?? "{}", ReceiverPartyId = item.ReceiverPartyId,
            AmountIn = item.AmountIn, CurrencyIn = item.CurrencyIn, CurrencyOut = item.CurrencyIn,
            Quantity = item.Quantity, UnitPrice = item.UnitPrice, ProductId = item.ProductId, Sku = item.Sku,
            NameSnapshot = item.NameSnapshot
        }).ToList();
        _dbContext.OrderItems.AddRange(current.Items);
        foreach (var role in current.PartyRoles.Where(role => role.Role == OrderPartyRoles.Payer).ToList())
        {
            _dbContext.OrderPartyRoles.Remove(role);
            current.PartyRoles.Remove(role);
        }
        if (payerPartyId is { } payer)
        {
            var role = new OrderPartyRole { TenantId = tenantId, OrderId = orderId, PartyId = payer, Role = OrderPartyRoles.Payer };
            current.PartyRoles.Add(role);
            _dbContext.OrderPartyRoles.Add(role);
        }
        current.PayerPartyId = payerPartyId;
        current.AmountIn = items.Sum(item => item.AmountIn);
        current.CurrencyIn = currency;
        current.ProvenanceJson = revision;
        current.UpdatedAt = _clock.UtcNow;
        _dbContext.Entry(current).Property(order => order.UpdatedAt).IsModified = true;
        var historyEvent = BuildHistoryEvent(tenantId, orderId, "CheckoutRefreshed", revision);
        current.HistoryEvents.Add(historyEvent);
        _dbContext.OrderHistoryEvents.Add(historyEvent);
        try { await _dbContext.SaveChangesAsync(cancellationToken); }
        catch
        {
            foreach (var entry in _dbContext.ChangeTracker.Entries().Where(entry =>
                         entry.Entity is Order order && order.Id == orderId && order.TenantId == tenantId
                         || entry.Entity is OrderItem item && item.OrderId == orderId && item.TenantId == tenantId
                         || entry.Entity is OrderPartyRole role && role.OrderId == orderId && role.TenantId == tenantId
                         || entry.Entity is OrderHistoryEvent history && history.OrderId == orderId && history.TenantId == tenantId).ToList())
                entry.State = EntityState.Detached;
            throw;
        }
        return MapToDto(current);
    }

    private void DetachOrderGraph(Order order, IEnumerable<EntityEntry<OutboxMessage>> enqueuedOutbox)
    {
        // The whole graph was cascade-tracked as Added by _dbContext.Orders.Add. After a failed
        // insert we detach every node so the rejected order can't be replayed by a later
        // SaveChanges on this scoped context.
        foreach (var historyEvent in order.HistoryEvents.ToArray())
        {
            _dbContext.Entry(historyEvent).State = EntityState.Detached;
        }

        foreach (var partyRole in order.PartyRoles.ToArray())
        {
            _dbContext.Entry(partyRole).State = EntityState.Detached;
        }

        foreach (var item in order.Items.ToArray())
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

    public async Task<IReadOnlyDictionary<Guid, PartyOrderAggregate>> GetPartyOrderAggregatesAsync(
        IReadOnlyCollection<Guid> payerPartyIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payerPartyIds);
        var ids = payerPartyIds.Distinct().ToList();
        // Every requested party is present, so a caller can index the result without
        // re-checking; no orders reads as an explicit zero rather than a missing key.
        var result = ids.ToDictionary(id => id, _ => PartyOrderAggregate.Empty);
        if (ids.Count == 0)
        {
            return result;
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Grouped in the DATABASE, spine-wide (no OrderType filter — ADR-011): one row per
        // (party, currency) carrying that pair's order count and value. The per-party count is
        // summed from these rows because an order has exactly one CurrencyIn, so the currency
        // groups partition the party's orders without overlap.
        var grouped = await _dbContext.Orders.AsNoTracking()
            .Where(o => o.TenantId == tenantId && o.PayerPartyId != null && ids.Contains(o.PayerPartyId.Value))
            .GroupBy(o => new { PartyId = o.PayerPartyId!.Value, o.CurrencyIn })
            .Select(g => new
            {
                g.Key.PartyId,
                g.Key.CurrencyIn,
                Count = g.Count(),
                Amount = g.Sum(o => o.AmountIn),
            })
            .ToListAsync(cancellationToken);

        foreach (var partyGroup in grouped.GroupBy(x => x.PartyId))
        {
            result[partyGroup.Key] = new PartyOrderAggregate(
                partyGroup.Sum(x => x.Count),
                partyGroup
                    .OrderBy(x => x.CurrencyIn, StringComparer.Ordinal)
                    .Select(x => new OrderCurrencyTotal(x.CurrencyIn, x.Amount))
                    .ToList());
        }

        return result;
    }

    public async Task<PagedResult<OrderSummary>> ListAsync(ListOrdersQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var (pageNumber, pageSize) = NormalizePaging(query);

        var orders = ApplyListFilters(_dbContext.Orders.AsNoTracking(), query);

        var totalCount = await orders.CountAsync(cancellationToken);

        // CreatedAt alone is not a total order — on ties SQL Server may order rows differently
        // between page queries, so a multi-page window walk could skip or double-count an order.
        // Id breaks the tie deterministically (same ordering as ListWithItemsAsync).
        var items = await orders
            .OrderByDescending(o => o.CreatedAt)
            .ThenBy(o => o.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderSummary(
                o.Id, o.OrderType, o.Status, o.AmountIn, o.CurrencyIn, o.CreatedAt, o.Items.Count, o.OrderNumber))
            .ToListAsync(cancellationToken);

        return new PagedResult<OrderSummary>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<PagedResult<OrderDto>> ListWithItemsAsync(ListOrdersQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var (pageNumber, pageSize) = NormalizePaging(query);

        // Same filters as ListAsync (one predicate, Spec 055 §9's centralisation), but the page is
        // materialised with line items so per-line retail fields can be aggregated. Same
        // deterministic (CreatedAt DESC, Id) ordering too — CreatedAt ties must not let a window
        // walk skip or double-count an order between page queries.
        var orders = ApplyListFilters(_dbContext.Orders.AsNoTracking(), query);

        var totalCount = await orders.CountAsync(cancellationToken);

        var page = await orders
            .OrderByDescending(o => o.CreatedAt)
            .ThenBy(o => o.Id)
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
        if (query.OrderIds is { Count: > 0 } orderIds)
        {
            orders = orders.Where(o => orderIds.Contains(o.Id));
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
        if (!await _dbContext.Orders.AnyAsync(o => o.Id == orderId && o.TenantId == tenantId, cancellationToken))
            throw new KeyNotFoundException($"Order {orderId} was not found.");

        // Preserve existing randomly keyed links; retries must not replace their audit identity.
        if (await _dbContext.OrderFundingRefs.AsNoTracking().AnyAsync(existing => existing.TenantId == tenantId
                && existing.OrderId == orderId && existing.PaymentIntentId == paymentIntentId, cancellationToken))
            return;

        // The existing primary key arbitrates simultaneous inserts without another schema/index.
        var identity = Encoding.UTF8.GetBytes($"Aonik.OrderFundingRef:v1:{tenantId:N}:{orderId:N}:{paymentIntentId:N}");
        var funding = new OrderFundingRef
        {
            Id = new Guid(SHA256.HashData(identity).AsSpan(0, 16)),
            TenantId = tenantId,
            OrderId = orderId,
            PaymentIntentId = paymentIntentId
        };
        _dbContext.OrderFundingRefs.Add(funding);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _dbContext.Entry(funding).State = EntityState.Detached;
            if (!await _dbContext.OrderFundingRefs.AsNoTracking().AnyAsync(existing => existing.Id == funding.Id
                    && existing.TenantId == tenantId && existing.OrderId == orderId
                    && existing.PaymentIntentId == paymentIntentId, cancellationToken))
                throw;
        }
        catch
        {
            _dbContext.Entry(funding).State = EntityState.Detached;
            throw;
        }
    }

    public async Task LinkFulfilmentAsync(Guid orderId, OrderFulfilmentLink link, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(link);
        var set = new[] { link.PayoutId, link.PaymentIntentId, link.PartnerBillPaymentId, link.SubscriptionPeriodId }
            .Count(id => id is not null);
        if (set != 1)
        {
            throw new ArgumentException("Exactly one fulfilment reference must be set.", nameof(link));
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        await EnsureOrderExistsAsync(orderId, cancellationToken);

        // Idempotent. Settlement links before it commits so a crash leaves the period retryable,
        // and that retry must not accumulate a second identical fulfilment row against the same
        // order — the trace is a fact about what happened, not a log of how many times we said it.
        var alreadyLinked = await _dbContext.OrderFulfilmentRefs.AnyAsync(
            existing => existing.TenantId == tenantId
                && existing.OrderId == orderId
                && existing.PayoutId == link.PayoutId
                && existing.PaymentIntentId == link.PaymentIntentId
                && existing.PartnerBillPaymentId == link.PartnerBillPaymentId
                && existing.SubscriptionPeriodId == link.SubscriptionPeriodId,
            cancellationToken);

        if (alreadyLinked)
        {
            return;
        }

        _dbContext.OrderFulfilmentRefs.Add(new OrderFulfilmentRef
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = orderId,
            PayoutId = link.PayoutId,
            PaymentIntentId = link.PaymentIntentId,
            PartnerBillPaymentId = link.PartnerBillPaymentId,
            SubscriptionPeriodId = link.SubscriptionPeriodId
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
                    i.ReceiverPartyId, i.Quantity, i.UnitPrice, i.ProductId, i.Sku, i.DetailsJson, i.NameSnapshot))
                .ToList(), order.OrderNumber);

    private static void ValidateNames(IReadOnlyList<OrderItemCommand> items)
    {
        if (items.Any(item => item.NameSnapshot?.Length > 256))
            throw new InvalidStateException("Purchased item names cannot exceed 256 characters.");
    }
}
