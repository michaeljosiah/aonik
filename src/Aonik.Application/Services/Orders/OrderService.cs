using System.Text.Json;
using Microsoft.EntityFrameworkCore;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Orders;
using Aonik.Application.Services.Compliance;
using Aonik.Application.Services.Parties;
using Aonik.Domain.Orders.Entities;
using Aonik.Domain.Pricing.Entities;
using Aonik.SharedKernel.Abstractions;
using PartyEntity = Aonik.Domain.Party.Entities.Party;

namespace Aonik.Application.Services.Orders;

public class OrderService : IOrderService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IPartyService _partyService;
    private readonly IComplianceService _complianceService;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;

    public OrderService(
        IAonikDbContext dbContext,
        ITenantProvider tenantProvider,
        IPartyService partyService,
        IComplianceService complianceService,
        IAuditLogWriter auditLogWriter,
        IClock clock,
        ICurrentUserProvider currentUserProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _partyService = partyService;
        _complianceService = complianceService;
        _auditLogWriter = auditLogWriter;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
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

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await _dbContext.Orders
                .Include(order => order.Items)
                .FirstOrDefaultAsync(
                    order => order.OrderType == "BillPayment" && order.IdempotencyKey == request.IdempotencyKey,
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
            IdempotencyKey = request.IdempotencyKey?.Trim(),
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
            Role = "Payer",
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
        await _dbContext.SaveChangesAsync(cancellationToken);

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

        order.Items.Add(created.OrderItem);
        order.PartyRoles.Add(created.ReceiverRole);
        order.HistoryEvents.Add(BuildHistoryEvent(order.Id, "ItemAdded"));

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
        order.HistoryEvents.Add(BuildHistoryEvent(order.Id, "ItemUpdated"));

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
            .Where(role => role.Role == "Receiver")
            .Where(role => DetailsHasOrderItem(role.DetailsJson, orderItemId))
            .ToList();

        foreach (var role in receiverRoles)
        {
            order.PartyRoles.Remove(role);
        }

        order.HistoryEvents.Add(BuildHistoryEvent(order.Id, "ItemRemoved"));
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
        order.HistoryEvents.Add(BuildHistoryEvent(order.Id, "OrderSubmitted"));

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

        return await MapOrderAsync(order, cancellationToken);
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
        order.HistoryEvents.Add(BuildHistoryEvent(order.Id, "OrderCancelled", reason));
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

        var pricingQuote = await LoadPricingQuoteAsync(request.PricingQuoteId, cancellationToken);
        var receiver = await ResolveReceiverAsync(order, request, cancellationToken);

        var details = new BillPaymentItemDetails(
            request.BillerId,
            biller.Name,
            request.ServiceId,
            request.ServiceCode,
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
            Role = "Receiver",
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
            new Models.Party.CreatePartyRequest(
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
                new Models.Party.CreatePartyRelationshipRequest(
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

    private async Task<PartyEntity?> LoadPartyAsync(Guid partyId, CancellationToken cancellationToken)
    {
        return await _dbContext.Parties
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == partyId, cancellationToken);
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

    private async Task<BillPaymentOrderResponse> MapOrderAsync(Order order, CancellationToken cancellationToken, PartyEntity? payer = null)
    {
        payer ??= await LoadPartyAsync(order.PayerPartyId ?? Guid.Empty, cancellationToken);
        var items = new List<OrderItemResponse>();
        foreach (var item in order.Items.OrderBy(entity => entity.ItemIndex))
        {
            items.Add(await MapOrderItemAsync(item, cancellationToken));
        }

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
        var receiver = await LoadPartyAsync(details.ReceiverPartyId, cancellationToken);
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
            .FirstOrDefault(r => r.Role == "Receiver" && DetailsHasOrderItem(r.DetailsJson, orderItemId));

        if (role == null)
        {
            order.PartyRoles.Add(new OrderPartyRole
            {
                OrderId = order.Id,
                PartyId = receiverPartyId,
                Role = "Receiver",
                DetailsJson = JsonSerializer.Serialize(new { orderItemId }, JsonOptions)
            });
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
