using Microsoft.EntityFrameworkCore;

using Aonik.Finance.Persistence;
using Aonik.Finance.Entities.Orders;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Finance.Services.Orders;

/// <summary>
/// Spec 041 / ADR-011 Phase 2 — the type-agnostic implementation of the core
/// <see cref="IOrderService"/> contract. Owns the generic order spine (create, read, list,
/// transitions, funding/fulfilment links) for every <c>OrderType</c>, including
/// <c>ProductPurchase</c>. Type-specific creation (bill payment, remittance) continues to live in
/// <see cref="OrderService"/> and will compose this. The implementation lives in Finance for now;
/// Phase 3 relocates it to <c>Aonik.Ordering</c> behind the same SharedKernel contract.
/// </summary>
internal sealed class CoreOrderService : IOrderService
{
    private readonly FinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;

    public CoreOrderService(
        FinanceDbContext dbContext,
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

        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            var existing = await _dbContext.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(
                    o => o.OrderType == command.OrderType && o.IdempotencyKey == command.IdempotencyKey,
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
            IdempotencyKey = command.IdempotencyKey,
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

        order.HistoryEvents.Add(BuildHistoryEvent(tenantId, orderId, "Created", string.Empty));

        _dbContext.Orders.Add(order);
        _dbContext.EnqueueIntegrationEvent(new OrderCreatedEvent(
            tenantId, orderId, order.OrderType, order.PayerPartyId, order.AmountIn, order.CurrencyIn));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(order);
    }

    public async Task<OrderDto?> GetAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        return order is null ? null : MapToDto(order);
    }

    public async Task<PagedResult<OrderSummary>> ListAsync(ListOrdersQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize is < 1 or > 200 ? 20 : query.PageSize;

        var orders = _dbContext.Orders.AsNoTracking();

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

    public async Task<OrderDto> TransitionAsync(Guid orderId, string toStatus, string? reason = null, CancellationToken cancellationToken = default)
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
