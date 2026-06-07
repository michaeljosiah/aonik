using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Finance.Persistence;
using Aonik.Finance.Contracts.Models.Orders;
using Aonik.Finance.Contracts.Services.Orders;
using Aonik.Finance.Entities;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Pricing;
using Aonik.Finance.Services.Observability;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Finance.Services.Orders;

internal class OrderService : IOrderService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly IReadOnlyDictionary<Guid, PartyReadModel> EmptyParties =
        new Dictionary<Guid, PartyReadModel>();

    private readonly FinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IPartyService _partyService;
    private readonly IComplianceService _complianceService;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        FinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        IPartyService partyService,
        IComplianceService complianceService,
        IAuditLogWriter auditLogWriter,
        IClock clock,
        ICurrentUserProvider currentUserProvider,
        ILogger<OrderService> logger)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _partyService = partyService;
        _complianceService = complianceService;
        _auditLogWriter = auditLogWriter;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<PagedResult<OrderListItem>> ListOrdersAsync(
        ListOrdersRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var query = _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(order => order.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.OrderType))
        {
            var orderType = request.OrderType.Trim();
            query = query.Where(order => order.OrderType == orderType);
        }

        if (request.PayerPartyId.HasValue)
        {
            var payerPartyId = request.PayerPartyId.Value;
            query = query.Where(order => order.PayerPartyId == payerPartyId);
        }

        if (request.CreatedFromUtc.HasValue)
        {
            var fromUtc = request.CreatedFromUtc.Value;
            query = query.Where(order => order.CreatedAt >= fromUtc);
        }

        if (request.CreatedToUtc.HasValue)
        {
            var toUtc = request.CreatedToUtc.Value;
            query = query.Where(order => order.CreatedAt < toUtc);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(order =>
                order.Id.ToString().Contains(search) ||
                (order.PayerPartyId.HasValue && _dbContext.Parties.Any(p =>
                    p.TenantId == tenantId && p.Id == order.PayerPartyId.Value && p.DisplayName.Contains(search))));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(order => order.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(order => new
            {
                order.Id,
                order.OrderType,
                order.Status,
                order.PayerPartyId,
                order.OriginCountry,
                order.CurrencyIn,
                order.AmountIn,
                order.AmountOut,
                order.CurrencyOut,
                order.CreatedAt,
                order.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return new PagedResult<OrderListItem>(
                Items: new List<OrderListItem>(),
                TotalCount: totalCount,
                PageNumber: pageNumber,
                PageSize: pageSize);
        }

        var payerPartyIds = rows
            .Select(r => r.PayerPartyId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var payerNamesById = payerPartyIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _dbContext.Parties
                .AsNoTracking()
                .Where(p => p.TenantId == tenantId && payerPartyIds.Contains(p.Id))
                .Select(p => new { p.Id, p.DisplayName })
                .ToDictionaryAsync(p => p.Id, p => p.DisplayName, cancellationToken);

        var items = rows.Select(r =>
        {
            var payerName = string.Empty;
            if (r.PayerPartyId.HasValue)
            {
                payerNamesById.TryGetValue(r.PayerPartyId.Value, out payerName);
            }

            return new OrderListItem(
                r.Id,
                r.OrderType,
                r.Status,
                r.PayerPartyId,
                payerName ?? string.Empty,
                r.OriginCountry,
                r.CurrencyIn,
                r.AmountIn,
                r.AmountOut,
                r.CurrencyOut,
                r.CreatedAt,
                r.UpdatedAt);
        }).ToList();

        return new PagedResult<OrderListItem>(
            items,
            totalCount,
            pageNumber,
            pageSize);
    }

    public async Task<BillPaymentOrderResponse> CreateBillPaymentOrderAsync(
        CreateBillPaymentOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.OriginCountry))
        {
            throw new ArgumentException("Origin country is required.", nameof(request.OriginCountry));
        }

        if (string.IsNullOrWhiteSpace(request.OriginCurrency))
        {
            throw new ArgumentException("Origin currency is required.", nameof(request.OriginCurrency));
        }

        // Normalize the idempotency key ONCE so the dedupe lookup and the stored
        // value can never diverge. Previously the lookup compared the raw request
        // key while the order persisted a trimmed copy, so a retry carrying
        // trailing whitespace missed the pre-check and created a duplicate order.
        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? null
            : request.IdempotencyKey.Trim();

        if (idempotencyKey != null)
        {
            var existing = await _dbContext.Orders
                .Include(order => order.Items)
                .FirstOrDefaultAsync(
                    order => order.OrderType == "BillPayment" && order.IdempotencyKey == idempotencyKey,
                    cancellationToken);

            if (existing != null)
            {
                return await MapOrderAsync(existing, cancellationToken);
            }
        }

        var payer = await LoadPartyAsync(request.PayerPartyId, cancellationToken);
        if (payer == null)
        {
            throw new InvalidOperationException($"Payer party {request.PayerPartyId} not found.");
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantProvider.GetCurrentTenantId(),
            OrderType = "BillPayment",
            Status = "Draft",
            IdempotencyKey = idempotencyKey,
            PayerPartyId = request.PayerPartyId,
            OriginCountry = request.OriginCountry.Trim().ToUpperInvariant(),
            CurrencyIn = request.OriginCurrency.Trim().ToUpperInvariant(),
            PurposeCode = request.PurposeCode?.Trim(),
            ProvenanceJson = "{}",
            FeesJson = "[]"
        };

        order.PartyRoles.Add(new OrderPartyRole
        {
            OrderId = order.Id,
            PartyId = request.PayerPartyId,
            Role = OrderPartyRoles.Payer,
            DetailsJson = "{}"
        });

        if (request.Items != null && request.Items.Count > 0)
        {
            var index = 0;
            foreach (var item in request.Items)
            {
                var created = await BuildOrderItemAsync(order, item, index++, cancellationToken);
                order.Items.Add(created.OrderItem);
                order.PartyRoles.Add(created.ReceiverRole);
                order.HistoryEvents.Add(BuildHistoryEvent(order.Id, "ItemAdded"));
            }
        }

        UpdateOrderTotals(order, await LoadPricingQuotesAsync(order.Items, cancellationToken));
        order.HistoryEvents.Add(BuildHistoryEvent(order.Id, "OrderCreated"));

        _dbContext.Orders.Add(order);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (idempotencyKey != null)
        {
            // Lost an idempotency race: a concurrent create with the same
            // (TenantId, OrderType, IdempotencyKey) committed first and tripped the
            // filtered unique index. Detach our rejected graph and return the
            // winning order so the caller still receives a single coherent result.
            DetachOrderGraph(order);

            var winner = await _dbContext.Orders
                .Include(entity => entity.Items)
                .FirstOrDefaultAsync(
                    entity => entity.OrderType == "BillPayment" && entity.IdempotencyKey == idempotencyKey,
                    cancellationToken);

            if (winner == null)
            {
                throw;
            }

            return await MapOrderAsync(winner, cancellationToken);
        }

        await _auditLogWriter.LogAsync(
            AuditEventNames.OrderCreated,
            "Order",
            order.Id,
            order.TenantId,
            _currentUserProvider.GetCurrentUserId(),
            correlationId: null,
            detailsJson: JsonSerializer.Serialize(new { OrderId = order.Id, order.OrderType }, JsonOptions),
            cancellationToken: cancellationToken);

        return await MapOrderAsync(order, cancellationToken, payer);
    }

    public async Task<BillPaymentOrderResponse> GetOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(entity => entity.Items)
            .Include(entity => entity.PartyRoles)
            .Include(entity => entity.HistoryEvents)
            .FirstOrDefaultAsync(entity => entity.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new InvalidOperationException($"Order {orderId} not found.");
        }

        return await MapOrderAsync(order, cancellationToken);
    }

    public async Task<OrderItemResponse> AddBillPaymentItemAsync(
        Guid orderId,
        CreateBillPaymentItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await LoadOrderForUpdateAsync(orderId, cancellationToken);
        EnsureDraft(order);

        var nextIndex = order.Items.Count == 0 ? 0 : order.Items.Max(item => item.ItemIndex) + 1;
        var created = await BuildOrderItemAsync(order, request, nextIndex, cancellationToken);

        // Explicitly add to DbSets so EF Core tracks these as Added (INSERT).
        // Adding only to navigation collections can cause EF to treat entities
        // as Modified (UPDATE) when the primary key is client-generated.
        _dbContext.OrderItems.Add(created.OrderItem);
        _dbContext.OrderPartyRoles.Add(created.ReceiverRole);
        var addedEvent = BuildHistoryEvent(order.Id, "ItemAdded");
        _dbContext.OrderHistoryEvents.Add(addedEvent);

        order.Items.Add(created.OrderItem);
        order.PartyRoles.Add(created.ReceiverRole);
        order.HistoryEvents.Add(addedEvent);

        UpdateOrderTotals(order, await LoadPricingQuotesAsync(order.Items, cancellationToken));
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.OrderItemAdded,
            "OrderItem",
            created.OrderItem.Id,
            order.TenantId,
            _currentUserProvider.GetCurrentUserId(),
            correlationId: null,
            detailsJson: JsonSerializer.Serialize(new { OrderId = order.Id, OrderItemId = created.OrderItem.Id }, JsonOptions),
            cancellationToken: cancellationToken);

        return await MapOrderItemAsync(created.OrderItem, cancellationToken);
    }

    public async Task<OrderItemResponse> UpdateBillPaymentItemAsync(
        Guid orderId,
        Guid orderItemId,
        UpdateBillPaymentItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await LoadOrderForUpdateAsync(orderId, cancellationToken);
        EnsureDraft(order);

        var item = order.Items.FirstOrDefault(entity => entity.Id == orderItemId);
        if (item == null)
        {
            throw new InvalidOperationException($"Order item {orderItemId} not found.");
        }

        var details = DeserializeDetails(item.DetailsJson);

        if (request.ServiceFieldValues != null)
        {
            details = details with { ServiceFieldValues = request.ServiceFieldValues };
        }

        if (!string.IsNullOrWhiteSpace(request.RelationshipTypeCode))
        {
            details = details with { RelationshipTypeCode = request.RelationshipTypeCode?.Trim() };
        }

        if (!string.IsNullOrWhiteSpace(request.PurposeCode))
        {
            details = details with { PurposeCode = request.PurposeCode?.Trim() };
        }

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            details = details with { Notes = request.Notes?.Trim() };
        }

        if (request.ReceiverPartyId.HasValue)
        {
            details = details with { ReceiverPartyId = request.ReceiverPartyId.Value };
            item.ReceiverPartyId = request.ReceiverPartyId.Value;
            UpdateReceiverRole(order, item.Id, request.ReceiverPartyId.Value);
        }

        if (request.PricingQuoteId.HasValue)
        {
            var pricingQuote = await LoadPricingQuoteAsync(request.PricingQuoteId.Value, cancellationToken);
            details = details with { PricingSnapshot = BuildPricingSnapshot(pricingQuote) };
            item.PricingQuoteId = pricingQuote.Id;
            item.AmountIn = pricingQuote.TotalAmount;
            item.AmountOut = pricingQuote.DestinationAmount;
            item.CurrencyIn = pricingQuote.OriginCurrency;
            item.CurrencyOut = pricingQuote.DestinationCurrency;
            item.FeesTotal = pricingQuote.FeesTotal;
            item.Status = ResolveItemStatus(pricingQuote);
        }

        item.DetailsJson = JsonSerializer.Serialize(details, JsonOptions);
        UpdateOrderTotals(order, await LoadPricingQuotesAsync(order.Items, cancellationToken));
        var updatedEvent = BuildHistoryEvent(order.Id, "ItemUpdated");
        _dbContext.OrderHistoryEvents.Add(updatedEvent);
        order.HistoryEvents.Add(updatedEvent);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.OrderItemUpdated,
            "OrderItem",
            item.Id,
            order.TenantId,
            _currentUserProvider.GetCurrentUserId(),
            correlationId: null,
            detailsJson: JsonSerializer.Serialize(new { OrderId = order.Id, OrderItemId = item.Id }, JsonOptions),
            cancellationToken: cancellationToken);

        return await MapOrderItemAsync(item, cancellationToken);
    }

    public async Task RemoveBillPaymentItemAsync(
        Guid orderId,
        Guid orderItemId,
        CancellationToken cancellationToken = default)
    {
        var order = await LoadOrderForUpdateAsync(orderId, cancellationToken);
        EnsureDraft(order);

        var item = order.Items.FirstOrDefault(entity => entity.Id == orderItemId);
        if (item == null)
        {
            throw new InvalidOperationException($"Order item {orderItemId} not found.");
        }

        order.Items.Remove(item);

        var receiverRoles = order.PartyRoles
            .Where(role => role.Role == OrderPartyRoles.Receiver)
            .Where(role => DetailsHasOrderItem(role.DetailsJson, orderItemId))
            .ToList();

        foreach (var role in receiverRoles)
        {
            order.PartyRoles.Remove(role);
        }

        var removedEvent = BuildHistoryEvent(order.Id, "ItemRemoved");
        _dbContext.OrderHistoryEvents.Add(removedEvent);
        order.HistoryEvents.Add(removedEvent);
        UpdateOrderTotals(order, await LoadPricingQuotesAsync(order.Items, cancellationToken));
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.OrderItemRemoved,
            "OrderItem",
            item.Id,
            order.TenantId,
            _currentUserProvider.GetCurrentUserId(),
            correlationId: null,
            detailsJson: JsonSerializer.Serialize(new { OrderId = order.Id, OrderItemId = item.Id }, JsonOptions),
            cancellationToken: cancellationToken);
    }

    public async Task<BillPaymentOrderResponse> SubmitOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        // Observability for Issue #142. BeginOrderScope so every child log
        // (EF queries, compliance service, audit writer) carries OrderId.
        // The span gives App Insights a trace; the OrderConfirmed event
        // carries the PricingQuoteId for chaining back to the Quote stage.
        using var orderScope = _logger.BeginOrderScope(orderId);
        using var activity = FinanceActivitySource.Source.StartActivity("order.confirm");
        activity?.SetTag(FinanceActivitySource.StageTag, MoneyActionStages.Confirm);
        activity?.SetTag(FinanceActivitySource.OrderIdTag, orderId);

        var tenantId = _tenantProvider.GetCurrentTenantId();
        activity?.SetTag(FinanceActivitySource.TenantIdTag, tenantId);

        try
        {
            var order = await LoadOrderForUpdateAsync(orderId, cancellationToken);
            EnsureDraft(order);

            if (order.Items.Count == 0)
            {
                throw new InvalidOperationException("At least one order item is required.");
            }

            var quotes = await LoadPricingQuotesAsync(order.Items, cancellationToken);
            var expiredItems = order.Items
                .Where(item => item.PricingQuoteId.HasValue)
                .Where(item => quotes.TryGetValue(item.PricingQuoteId!.Value, out var quote) && quote.ExpiresAt <= _clock.UtcNow)
                .Select(item => item.Id)
                .ToList();

            if (expiredItems.Count > 0)
            {
                throw new InvalidOperationException("One or more pricing quotes have expired.");
            }

            var requiresReview = await _complianceService.RequiresComplianceReviewAsync(order.Id, cancellationToken);
            order.Status = requiresReview ? "PendingCompliance" : "Submitted";
            var submittedEvent = BuildHistoryEvent(order.Id, "OrderSubmitted");
            _dbContext.OrderHistoryEvents.Add(submittedEvent);
            order.HistoryEvents.Add(submittedEvent);

            if (requiresReview)
            {
                await _complianceService.CreateOrderReviewCaseAsync(order.Id, cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditLogWriter.LogAsync(
                AuditEventNames.OrderSubmitted,
                "Order",
                order.Id,
                order.TenantId,
                _currentUserProvider.GetCurrentUserId(),
                correlationId: null,
                detailsJson: JsonSerializer.Serialize(new { OrderId = order.Id, order.Status }, JsonOptions),
                cancellationToken: cancellationToken);

            // Pull the first PricingQuoteId from the order's items — this is the
            // join key that lets the saved KQL query chain OrderId back to the
            // Quote-stage entries (which only carry PricingQuoteId, not OrderId).
            var firstQuoteId = order.Items
                .Select(item => item.PricingQuoteId)
                .FirstOrDefault(id => id.HasValue);

            activity?.SetTag(FinanceActivitySource.OutcomeTag, MoneyActionOutcomes.Success);
            if (firstQuoteId.HasValue)
            {
                activity?.SetTag(FinanceActivitySource.PricingQuoteIdTag, firstQuoteId.Value);
            }
            _logger.OrderConfirmed(order.Id, order.TenantId, $"Status={order.Status}", firstQuoteId);

            return await MapOrderAsync(order, cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.SetTag(FinanceActivitySource.OutcomeTag, MoneyActionOutcomes.Rejected);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.OrderRejected(orderId, tenantId, ex.Message);
            throw;
        }
    }

    public async Task<BillPaymentOrderResponse> CancelOrderAsync(
        Guid orderId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var order = await LoadOrderForUpdateAsync(orderId, cancellationToken);
        if (order.Status is "Cancelled" or "Completed" or "Failed")
        {
            return await MapOrderAsync(order, cancellationToken);
        }

        order.Status = "Cancelled";
        var cancelledEvent = BuildHistoryEvent(order.Id, "OrderCancelled", reason);
        _dbContext.OrderHistoryEvents.Add(cancelledEvent);
        order.HistoryEvents.Add(cancelledEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.OrderCancelled,
            "Order",
            order.Id,
            order.TenantId,
            _currentUserProvider.GetCurrentUserId(),
            correlationId: null,
            detailsJson: JsonSerializer.Serialize(new { OrderId = order.Id, reason }, JsonOptions),
            cancellationToken: cancellationToken);

        return await MapOrderAsync(order, cancellationToken);
    }

    private void DetachOrderGraph(Order order)
    {
        // The whole graph was cascade-tracked as Added by _dbContext.Orders.Add.
        // After a failed insert we detach every node so the rejected order can't
        // be replayed by a later SaveChanges on this scoped context.
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
    }

    private async Task<Order> LoadOrderForUpdateAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .Include(entity => entity.Items)
            .Include(entity => entity.PartyRoles)
            .Include(entity => entity.HistoryEvents)
            .FirstOrDefaultAsync(entity => entity.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new InvalidOperationException($"Order {orderId} not found.");
        }

        return order;
    }

    private void EnsureDraft(Order order)
    {
        if (!string.Equals(order.Status, "Draft", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Operation only allowed on draft orders.");
        }
    }

    private async Task<OrderItemCreationResult> BuildOrderItemAsync(
        Order order,
        CreateBillPaymentItemRequest request,
        int itemIndex,
        CancellationToken cancellationToken)
    {
        var biller = await _dbContext.CatalogBillers
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == request.BillerId, cancellationToken);

        if (biller == null)
        {
            throw new InvalidOperationException($"Biller {request.BillerId} not found.");
        }

        var service = await _dbContext.CatalogBillerServices
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == request.ServiceId && entity.BillerId == request.BillerId, cancellationToken);

        if (service == null)
        {
            throw new InvalidOperationException($"Service {request.ServiceId} not found.");
        }

        var requestedServiceCode = request.ServiceCode?.Trim() ?? string.Empty;
        var resolvedServiceCode = string.IsNullOrWhiteSpace(service.ServiceCode)
            ? requestedServiceCode
            : service.ServiceCode.Trim();

        if (string.IsNullOrWhiteSpace(resolvedServiceCode))
        {
            throw new InvalidOperationException("Service code is required for the catalog service.");
        }

        if (!string.IsNullOrWhiteSpace(service.ServiceCode)
            && !string.Equals(resolvedServiceCode, requestedServiceCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Service code does not match the catalog service.");
        }

        var pricingQuote = await LoadPricingQuoteAsync(request.PricingQuoteId, cancellationToken);
        var receiver = await ResolveReceiverAsync(order, request, cancellationToken);

        var details = new BillPaymentItemDetails(
            request.BillerId,
            biller.Name,
            request.ServiceId,
            resolvedServiceCode,
            service.Name,
            request.ServiceFieldValues,
            order.PayerPartyId ?? Guid.Empty,
            receiver.PartyId,
            request.RelationshipTypeCode,
            request.PurposeCode,
            request.Notes,
            BuildPricingSnapshot(pricingQuote));

        var orderItem = new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            ItemType = "BillPaymentLine",
            ItemIndex = itemIndex,
            DetailsJson = JsonSerializer.Serialize(details, JsonOptions),
            Status = ResolveItemStatus(pricingQuote),
            ReceiverPartyId = receiver.PartyId,
            AmountIn = pricingQuote.TotalAmount,
            CurrencyIn = pricingQuote.OriginCurrency,
            AmountOut = pricingQuote.DestinationAmount,
            CurrencyOut = pricingQuote.DestinationCurrency,
            FeesTotal = pricingQuote.FeesTotal,
            PricingQuoteId = pricingQuote.Id
        };

        var receiverRole = new OrderPartyRole
        {
            OrderId = order.Id,
            PartyId = receiver.PartyId,
            Role = OrderPartyRoles.Receiver,
            DetailsJson = JsonSerializer.Serialize(new
            {
                orderItemId = orderItem.Id,
                relationshipTypeCode = request.RelationshipTypeCode
            }, JsonOptions)
        };

        return new OrderItemCreationResult(orderItem, receiverRole);
    }

    private async Task<(Guid PartyId, string DisplayName)> ResolveReceiverAsync(
        Order order,
        CreateBillPaymentItemRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ReceiverPartyId.HasValue)
        {
            var receiver = await LoadPartyAsync(request.ReceiverPartyId.Value, cancellationToken);
            if (receiver == null)
            {
                throw new InvalidOperationException($"Receiver party {request.ReceiverPartyId.Value} not found.");
            }

            return (receiver.Id, receiver.DisplayName);
        }

        if (request.NewReceiver == null)
        {
            throw new InvalidOperationException("Receiver details are required.");
        }

        var created = await _partyService.CreatePartyAsync(
            new CreatePartyRequest(
                request.NewReceiver.DisplayName,
                request.NewReceiver.PartyType,
                request.NewReceiver.FirstName,
                request.NewReceiver.LastName,
                request.NewReceiver.Phone,
                request.NewReceiver.Email,
                request.NewReceiver.CountryCode),
            cancellationToken);

        await _complianceService.ScreenPartyAsync(created.PartyId, "KYC", cancellationToken);

        if (order.PayerPartyId.HasValue && !string.IsNullOrWhiteSpace(request.RelationshipTypeCode))
        {
            await _partyService.CreateRelationshipAsync(
                new CreatePartyRelationshipRequest(
                    order.PayerPartyId.Value,
                    created.PartyId,
                    request.RelationshipTypeCode!,
                    request.Notes),
                cancellationToken);
        }

        return (created.PartyId, created.DisplayName);
    }

    private async Task<PricingQuote> LoadPricingQuoteAsync(Guid pricingQuoteId, CancellationToken cancellationToken)
    {
        var quote = await _dbContext.PricingQuotes
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == pricingQuoteId, cancellationToken);

        if (quote == null)
        {
            throw new InvalidOperationException($"Pricing quote {pricingQuoteId} not found.");
        }

        return quote;
    }

    private async Task<Dictionary<Guid, PricingQuote>> LoadPricingQuotesAsync(
        IEnumerable<OrderItem> items,
        CancellationToken cancellationToken)
    {
        var quoteIds = items
            .Where(item => item.PricingQuoteId.HasValue)
            .Select(item => item.PricingQuoteId!.Value)
            .Distinct()
            .ToList();

        if (quoteIds.Count == 0)
        {
            return new Dictionary<Guid, PricingQuote>();
        }

        return await _dbContext.PricingQuotes
            .AsNoTracking()
            .Where(quote => quoteIds.Contains(quote.Id))
            .ToDictionaryAsync(quote => quote.Id, cancellationToken);
    }

    private async Task<PartyReadModel?> LoadPartyAsync(Guid partyId, CancellationToken cancellationToken)
    {
        return await _dbContext.Parties
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == partyId, cancellationToken);
    }

    // H4: batch-load every party an order references (payer + each item's receiver) in a
    // single round-trip, keyed for in-memory resolution during mapping. Replaces the previous
    // per-line-item party lookup (N+1). Mirrors the accountMap pattern in
    // PersonalFinanceInsightsService.GetAccountBreakdownAsync.
    private async Task<IReadOnlyDictionary<Guid, PartyReadModel>> LoadPartiesAsync(
        IEnumerable<Guid> partyIds,
        CancellationToken cancellationToken)
    {
        var ids = partyIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return EmptyParties;
        }

        return await _dbContext.Parties
            .AsNoTracking()
            .Where(entity => ids.Contains(entity.Id))
            .ToDictionaryAsync(entity => entity.Id, cancellationToken);
    }

    private void UpdateOrderTotals(Order order, Dictionary<Guid, PricingQuote> quotes)
    {
        var items = order.Items;
        if (items.Count == 0)
        {
            order.AmountIn = 0m;
            order.AmountOut = 0m;
            order.CurrencyOut = null;
            order.FeesJson = "[]";
            order.FxQuoteId = null;
            return;
        }

        var totalAmountIn = items.Sum(item => item.AmountIn);
        var totalFees = items.Sum(item => item.FeesTotal);
        var totalAmountOut = items.Sum(item => item.AmountOut);
        var currenciesOut = items.Select(item => item.CurrencyOut).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        order.AmountIn = totalAmountIn;
        order.AmountOut = totalAmountOut;
        order.CurrencyOut = currenciesOut.Count == 1 ? currenciesOut[0] : null;
        order.FeesJson = JsonSerializer.Serialize(
            items.Select(item => new { orderItemId = item.Id, feesTotal = item.FeesTotal, currency = item.CurrencyIn }),
            JsonOptions);

        var fxRateIds = items
            .Where(item => item.PricingQuoteId.HasValue)
            .Select(item => quotes.TryGetValue(item.PricingQuoteId!.Value, out var quote) ? quote.FxRateId : (Guid?)null)
            .Where(id => id.HasValue)
            .Distinct()
            .ToList();

        order.FxQuoteId = fxRateIds.Count == 1 ? fxRateIds[0] : null;
    }

    private OrderHistoryEvent BuildHistoryEvent(Guid orderId, string eventType, string? reason = null)
    {
        var now = _clock.UtcNow;
        var actorId = _currentUserProvider.GetCurrentUserId() ?? Guid.Empty;
        var actorType = actorId == Guid.Empty ? "System" : "User";

        return new OrderHistoryEvent
        {
            OrderId = orderId,
            EventType = eventType,
            EventAt = now,
            ActorType = actorType,
            ActorId = actorId,
            DetailsJson = string.IsNullOrWhiteSpace(reason)
                ? "{}"
                : JsonSerializer.Serialize(new { reason }, JsonOptions)
        };
    }

    private async Task<BillPaymentOrderResponse> MapOrderAsync(Order order, CancellationToken cancellationToken, PartyReadModel? payer = null)
    {
        // Deserialize each item's details once, then batch-load every party this order
        // references (payer + each item's receiver) in a single round-trip. Previously each
        // line item issued its own party lookup during mapping (N+1); see finding H4.
        var mappedItems = order.Items
            .OrderBy(entity => entity.ItemIndex)
            .Select(entity => (Item: entity, Details: DeserializeDetails(entity.DetailsJson)))
            .ToList();

        var payerPartyId = order.PayerPartyId ?? Guid.Empty;
        var partyIds = mappedItems.Select(entry => entry.Details.ReceiverPartyId).ToList();
        if (payer == null)
        {
            partyIds.Add(payerPartyId);
        }

        var parties = await LoadPartiesAsync(partyIds, cancellationToken);

        if (payer == null && payerPartyId != Guid.Empty && parties.TryGetValue(payerPartyId, out var resolvedPayer))
        {
            payer = resolvedPayer;
        }

        var items = mappedItems
            .Select(entry => MapOrderItem(entry.Item, entry.Details, parties))
            .ToList();

        var totalFees = order.Items.Sum(entity => entity.FeesTotal);
        var submittedAt = order.HistoryEvents
            .Where(evt => evt.EventType == "OrderSubmitted")
            .OrderByDescending(evt => evt.EventAt)
            .Select(evt => (DateTime?)evt.EventAt)
            .FirstOrDefault();

        return new BillPaymentOrderResponse(
            order.Id,
            order.OrderType,
            order.Status,
            order.PayerPartyId ?? Guid.Empty,
            payer?.DisplayName ?? string.Empty,
            order.OriginCountry ?? string.Empty,
            order.CurrencyIn,
            order.AmountIn,
            totalFees,
            order.AmountOut ?? 0m,
            order.CurrencyOut,
            order.PurposeCode,
            order.CreatedAt,
            submittedAt,
            items);
    }

    private async Task<OrderItemResponse> MapOrderItemAsync(OrderItem item, CancellationToken cancellationToken)
    {
        var details = DeserializeDetails(item.DetailsJson);
        var parties = await LoadPartiesAsync(new[] { details.ReceiverPartyId }, cancellationToken);
        return MapOrderItem(item, details, parties);
    }

    private OrderItemResponse MapOrderItem(
        OrderItem item,
        BillPaymentItemDetails details,
        IReadOnlyDictionary<Guid, PartyReadModel> parties)
    {
        parties.TryGetValue(details.ReceiverPartyId, out var receiver);
        var quoteExpired = details.PricingSnapshot.QuoteExpiresAt.HasValue
            && details.PricingSnapshot.QuoteExpiresAt.Value <= _clock.UtcNow;

        return new OrderItemResponse(
            item.Id,
            item.ItemIndex,
            item.ItemType,
            item.Status,
            details.BillerId,
            details.BillerName,
            details.ServiceId,
            details.ServiceCode,
            details.ServiceName,
            details.ServiceFieldValues,
            details.ReceiverPartyId,
            receiver?.DisplayName ?? string.Empty,
            details.RelationshipTypeCode,
            item.AmountIn,
            item.CurrencyIn,
            item.AmountOut,
            item.CurrencyOut,
            item.FeesTotal,
            details.PricingSnapshot.ExchangeRate,
            item.PricingQuoteId,
            details.PricingSnapshot.QuoteExpiresAt,
            quoteExpired);
    }

    private void UpdateReceiverRole(Order order, Guid orderItemId, Guid receiverPartyId)
    {
        var role = order.PartyRoles
            .FirstOrDefault(r => r.Role == OrderPartyRoles.Receiver && DetailsHasOrderItem(r.DetailsJson, orderItemId));

        if (role == null)
        {
            var newRole = new OrderPartyRole
            {
                OrderId = order.Id,
                PartyId = receiverPartyId,
                Role = OrderPartyRoles.Receiver,
                DetailsJson = JsonSerializer.Serialize(new { orderItemId }, JsonOptions)
            };
            _dbContext.OrderPartyRoles.Add(newRole);
            order.PartyRoles.Add(newRole);
            return;
        }

        role.PartyId = receiverPartyId;
    }

    private string ResolveItemStatus(PricingQuote quote)
        => quote.ExpiresAt <= _clock.UtcNow ? "QuoteExpired" : "Valid";

    private static bool DetailsHasOrderItem(string detailsJson, Guid orderItemId)
    {
        if (string.IsNullOrWhiteSpace(detailsJson))
        {
            return false;
        }

        try
        {
            var data = JsonSerializer.Deserialize<ReceiverRoleDetails>(detailsJson, JsonOptions);
            return data?.OrderItemId == orderItemId;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static BillPaymentItemDetails DeserializeDetails(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new BillPaymentItemDetails();
        }

        return JsonSerializer.Deserialize<BillPaymentItemDetails>(json, JsonOptions) ?? new BillPaymentItemDetails();
    }

    private static PricingSnapshot BuildPricingSnapshot(PricingQuote quote)
    {
        var feeBreakdown = string.IsNullOrWhiteSpace(quote.FeeBreakdownJson)
            ? new List<FeeBreakdownSnapshot>()
            : JsonSerializer.Deserialize<List<FeeBreakdownSnapshot>>(quote.FeeBreakdownJson, JsonOptions)
                ?? new List<FeeBreakdownSnapshot>();

        return new PricingSnapshot(
            quote.Id,
            quote.FxRateId,
            quote.ExchangeRate,
            quote.RateMarkup,
            quote.PricingPolicyId,
            quote.PricingPolicyVersion,
            quote.RateTimestamp,
            quote.CreatedAt,
            quote.ExpiresAt,
            feeBreakdown);
    }

    private record OrderItemCreationResult(OrderItem OrderItem, OrderPartyRole ReceiverRole);

    private record BillPaymentItemDetails(
        Guid BillerId,
        string BillerName,
        Guid ServiceId,
        string ServiceCode,
        string ServiceName,
        Dictionary<string, string> ServiceFieldValues,
        Guid PayerPartyId,
        Guid ReceiverPartyId,
        string? RelationshipTypeCode,
        string? PurposeCode,
        string? Notes,
        PricingSnapshot PricingSnapshot)
    {
        public BillPaymentItemDetails()
            : this(Guid.Empty, string.Empty, Guid.Empty, string.Empty, string.Empty, new Dictionary<string, string>(), Guid.Empty, Guid.Empty, null, null, null, new PricingSnapshot())
        {
        }
    }

    private record PricingSnapshot(
        Guid PricingQuoteId,
        Guid FxRateId,
        decimal ExchangeRate,
        decimal RateMarkup,
        Guid PricingPolicyId,
        string PricingPolicyVersion,
        DateTime RateTimestamp,
        DateTime QuoteTimestamp,
        DateTime? QuoteExpiresAt,
        IReadOnlyCollection<FeeBreakdownSnapshot> FeeBreakdown)
    {
        public PricingSnapshot()
            : this(Guid.Empty, Guid.Empty, 0m, 0m, Guid.Empty, string.Empty, DateTime.UtcNow, DateTime.UtcNow, null, Array.Empty<FeeBreakdownSnapshot>())
        {
        }
    }

    private record FeeBreakdownSnapshot(
        string Code,
        string Description,
        string CalculationType,
        decimal Amount,
        string Currency);

    private record ReceiverRoleDetails(Guid OrderItemId);
}
