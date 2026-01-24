using Microsoft.EntityFrameworkCore;

using Aonik.Application.Abstractions;
using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Orders;
using Aonik.Application.Models.Pricing;
using Aonik.Application.Services.Identity;
using Aonik.Domain.Billing.Entities;
using Aonik.Domain.Orders;
using Aonik.Domain.Orders.Entities;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Orders;

public class OrderService : IOrderService
{
    private const string PayerRole = "Payer";
    private const string PayeeRole = "Payee";
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 50;
    private static readonly OrderDetails EmptyDetails = new(null, null, null);

    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IJsonSerializer _jsonSerializer;

    public OrderService(
        IAonikDbContext dbContext,
        ITenantProvider tenantProvider,
        IPermissionService permissionService,
        ICurrentUserProvider currentUserProvider,
        IJsonSerializer jsonSerializer)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _permissionService = permissionService;
        _currentUserProvider = currentUserProvider;
        _jsonSerializer = jsonSerializer;
    }

    public async Task<ValidateDuplicateOrderResponse> ValidateDuplicateAsync(
        ValidateDuplicateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Order.Read", cancellationToken);

        if (request.CustomerId == Guid.Empty)
        {
            throw new ArgumentException("CustomerId is required.", nameof(request.CustomerId));
        }

        var orderType = NormalizeOrderType(request.OrderType);
        var serviceCode = NormalizeServiceCode(request.ServiceCode);
        var currency = NormalizeCurrency(request.Currency);
        var amount = EnsurePositiveAmount(request.Amount, nameof(request.Amount));
        ValidateDetails(orderType, request.Details);

        var requestedAt = request.RequestedAt ?? DateTimeOffset.UtcNow;
        var windowStart = requestedAt.AddHours(-24);

        var candidates = await _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.OrderType == orderType.ToString()
                && order.ServiceCode == serviceCode
                && order.AmountIn == amount
                && order.CurrencyIn == currency
                && order.CreatedAt >= windowStart.UtcDateTime
                && order.CreatedAt <= requestedAt.UtcDateTime)
            .Where(order => order.PartyRoles.Any(role => role.Role == PayerRole && role.PartyId == request.CustomerId))
            .ToListAsync(cancellationToken);

        var match = candidates.FirstOrDefault(order => MatchesDetails(order, orderType, request.Details));

        if (match == null)
        {
            return new ValidateDuplicateOrderResponse(null, null, null, null, null, null);
        }

        return new ValidateDuplicateOrderResponse(
            match.Id,
            match.TenantId,
            match.OrderNumber,
            match.InvoiceId,
            match.Status,
            match.CreatedAt);
    }

    public async Task<CreateOrderResponse> CreateAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Order.Create", cancellationToken);

        if (request.CustomerId == Guid.Empty)
        {
            throw new ArgumentException("CustomerId is required.", nameof(request.CustomerId));
        }

        if (request.PricingQuoteId == Guid.Empty)
        {
            throw new ArgumentException("PricingQuoteId is required.", nameof(request.PricingQuoteId));
        }

        var orderType = NormalizeOrderType(request.OrderType);
        var serviceCode = NormalizeServiceCode(request.ServiceCode);
        var currency = NormalizeCurrency(request.Currency);
        var amount = EnsurePositiveAmount(request.Amount, nameof(request.Amount));
        var totalAmount = request.TotalAmount.HasValue
            ? EnsurePositiveAmount(request.TotalAmount.Value, nameof(request.TotalAmount))
            : (decimal?)null;

        ValidateDetails(orderType, request.Details);
        ValidateItems(request.Items);

        if (request.Payer != null && request.Payer.PartyId != request.CustomerId)
        {
            throw new InvalidOperationException("Payer must match the customerId.");
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var orderId = Guid.NewGuid();
        var orderNumber = GenerateOrderNumber();

        var partyRoles = BuildPartyRoles(orderId, tenantId, request, orderType);
        var orderItems = BuildOrderItems(orderId, tenantId, request.Items);
        var invoice = orderType == OrderType.BillPayment
            ? BuildInvoice(orderId, orderNumber, tenantId, request, orderItems)
            : null;

        var order = new Order
        {
            Id = orderId,
            TenantId = tenantId,
            OrderNumber = orderNumber,
            OrderType = orderType.ToString(),
            ServiceCode = serviceCode,
            AmountIn = amount,
            CurrencyIn = currency,
            AmountOut = totalAmount,
            CurrencyOut = totalAmount.HasValue ? currency : null,
            FeesJson = _jsonSerializer.Serialize(new OrderFeeSnapshot(request.FeesTotal, request.FeeBreakdown)),
            FxQuoteId = null,
            InvoiceId = invoice?.Id,
            Status = OrderStatuses.Pending,
            ProvenanceJson = _jsonSerializer.Serialize(new OrderPricingSnapshot(
                request.PricingQuoteId,
                request.ExchangeRate,
                request.RateMarkup,
                request.FeesTotal,
                totalAmount)),
            OrderDetailsJson = _jsonSerializer.Serialize(request.Details),
            MetadataJson = _jsonSerializer.Serialize(request.Metadata ?? new Dictionary<string, string>()),
            PartyRoles = partyRoles,
            Items = orderItems
        };

        _dbContext.Orders.Add(order);

        if (invoice != null)
        {
            _dbContext.Invoices.Add(invoice);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateOrderResponse(
            order.Id,
            order.TenantId,
            order.OrderNumber,
            order.InvoiceId,
            order.Status,
            order.CreatedAt,
            null,
            invoice?.Status);
    }

    public async Task<OrderDetailResponse?> GetAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Order.Read", cancellationToken);

        var order = await _dbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            return null;
        }

        var items = await _dbContext.OrderItems
            .AsNoTracking()
            .Where(item => item.OrderId == order.Id)
            .ToListAsync(cancellationToken);

        var paymentIntent = await _dbContext.PaymentIntents
            .AsNoTracking()
            .Where(intent => intent.OrderId == order.Id)
            .OrderByDescending(intent => intent.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var payoutRef = await _dbContext.OrderFulfilmentRefs
            .AsNoTracking()
            .Where(refs => refs.OrderId == order.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var payoutStatus = payoutRef == null
            ? null
            : await _dbContext.Payouts
                .AsNoTracking()
                .Where(payout => payout.Id == payoutRef.PayoutId)
                .Select(payout => payout.Status)
                .FirstOrDefaultAsync(cancellationToken);

        var invoiceStatus = order.InvoiceId.HasValue
            ? await _dbContext.Invoices
                .AsNoTracking()
                .Where(invoice => invoice.Id == order.InvoiceId.Value)
                .Select(invoice => invoice.Status)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return BuildDetailResponse(
            order,
            items,
            paymentIntent?.Id,
            paymentIntent?.Status,
            invoiceStatus,
            payoutRef?.PayoutId,
            payoutStatus);
    }

    public async Task<OrderListResponse> ListAsync(
        OrderListQuery query,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Order.Read", cancellationToken);

        var pageNumber = query.PageNumber <= 0 ? DefaultPageNumber : query.PageNumber;
        var pageSize = query.PageSize <= 0 ? DefaultPageSize : query.PageSize;

        IQueryable<Order> orderQuery = _dbContext.Orders.AsNoTracking();

        if (query.CustomerId.HasValue)
        {
            orderQuery = orderQuery.Where(order => order.PartyRoles
                .Any(role => role.Role == PayerRole && role.PartyId == query.CustomerId.Value));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            orderQuery = orderQuery.Where(order => order.Status == query.Status.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.OrderType))
        {
            var orderType = NormalizeOrderType(query.OrderType);
            orderQuery = orderQuery.Where(order => order.OrderType == orderType.ToString());
        }

        if (!string.IsNullOrWhiteSpace(query.ServiceCode))
        {
            var serviceCode = NormalizeServiceCode(query.ServiceCode);
            orderQuery = orderQuery.Where(order => order.ServiceCode == serviceCode);
        }

        if (query.DateFrom.HasValue)
        {
            orderQuery = orderQuery.Where(order => order.CreatedAt >= query.DateFrom.Value.UtcDateTime);
        }

        if (query.DateTo.HasValue)
        {
            orderQuery = orderQuery.Where(order => order.CreatedAt <= query.DateTo.Value.UtcDateTime);
        }

        var totalCount = await orderQuery.CountAsync(cancellationToken);

        var orders = await orderQuery
            .OrderByDescending(order => order.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var orderIds = orders.Select(order => order.Id).ToList();
        var invoiceIds = orders.Where(order => order.InvoiceId.HasValue).Select(order => order.InvoiceId!.Value).ToList();

        var paymentIntents = await _dbContext.PaymentIntents
            .AsNoTracking()
            .Where(intent => orderIds.Contains(intent.OrderId))
            .OrderByDescending(intent => intent.CreatedAt)
            .ToListAsync(cancellationToken);

        var invoiceStatuses = await _dbContext.Invoices
            .AsNoTracking()
            .Where(invoice => invoiceIds.Contains(invoice.Id))
            .ToDictionaryAsync(invoice => invoice.Id, invoice => invoice.Status, cancellationToken);

        var summaries = orders.Select(order =>
        {
            var intent = paymentIntents.FirstOrDefault(pi => pi.OrderId == order.Id);
            invoiceStatuses.TryGetValue(order.InvoiceId ?? Guid.Empty, out var invoiceStatus);
            return BuildSummaryResponse(order, intent?.Status, invoiceStatus);
        }).ToList();

        return new OrderListResponse(summaries, totalCount, pageNumber, pageSize);
    }

    private static string GenerateOrderNumber()
    {
        var year = DateTime.UtcNow.Year;
        var shortId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"ORD-{year}-{shortId}";
    }

    private static string NormalizeServiceCode(string serviceCode)
    {
        var normalized = string.IsNullOrWhiteSpace(serviceCode) ? string.Empty : serviceCode.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("ServiceCode is required.", nameof(serviceCode));
        }

        return normalized;
    }

    private static string NormalizeCurrency(string currency)
    {
        var normalized = string.IsNullOrWhiteSpace(currency) ? string.Empty : currency.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length != 3)
        {
            throw new ArgumentException("Currency must be a 3-letter ISO code.", nameof(currency));
        }

        return normalized;
    }

    private static decimal EnsurePositiveAmount(decimal amount, string paramName)
    {
        if (amount <= 0m)
        {
            throw new ArgumentException("Amount must be greater than zero.", paramName);
        }

        return amount;
    }

    private static void ValidateItems(IReadOnlyCollection<OrderItemRequest>? items)
    {
        if (items == null)
        {
            return;
        }

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.ItemType))
            {
                throw new ArgumentException("Order item type is required.", nameof(items));
            }

            if (string.IsNullOrWhiteSpace(item.Reference))
            {
                throw new ArgumentException("Order item reference is required.", nameof(items));
            }

            if (item.Amount <= 0m)
            {
                throw new ArgumentException("Order item amount must be greater than zero.", nameof(items));
            }

            NormalizeCurrency(item.Currency);
        }
    }

    private static OrderType NormalizeOrderType(string orderType)
    {
        if (string.IsNullOrWhiteSpace(orderType))
        {
            throw new ArgumentException("OrderType is required.", nameof(orderType));
        }

        if (!Enum.TryParse<OrderType>(orderType, true, out var parsed))
        {
            throw new ArgumentException("OrderType is invalid.", nameof(orderType));
        }

        return parsed;
    }

    private static void ValidateDetails(OrderType orderType, OrderDetails details)
    {
        switch (orderType)
        {
            case OrderType.BillPayment:
                if (details.BillPayment == null)
                {
                    throw new ArgumentException("BillPayment details are required.", nameof(details));
                }

                if (details.BillPayment.BillerId == Guid.Empty)
                {
                    throw new ArgumentException("BillerId is required for bill payments.", nameof(details));
                }

                if (string.IsNullOrWhiteSpace(details.BillPayment.BillReference))
                {
                    throw new ArgumentException("BillReference is required for bill payments.", nameof(details));
                }

                break;
            case OrderType.BankTransfer:
                if (details.BankTransfer == null)
                {
                    throw new ArgumentException("BankTransfer details are required.", nameof(details));
                }

                if (string.IsNullOrWhiteSpace(details.BankTransfer.DestinationAccountNumber))
                {
                    throw new ArgumentException("DestinationAccountNumber is required for bank transfers.", nameof(details));
                }

                if (string.IsNullOrWhiteSpace(details.BankTransfer.DestinationBankCode))
                {
                    throw new ArgumentException("DestinationBankCode is required for bank transfers.", nameof(details));
                }

                if (string.IsNullOrWhiteSpace(details.BankTransfer.DestinationCountry))
                {
                    throw new ArgumentException("DestinationCountry is required for bank transfers.", nameof(details));
                }

                break;
            case OrderType.CashCollection:
                if (details.CashCollection == null)
                {
                    throw new ArgumentException("CashCollection details are required.", nameof(details));
                }

                if (details.CashCollection.RecipientId == Guid.Empty)
                {
                    throw new ArgumentException("RecipientId is required for cash collection.", nameof(details));
                }

                break;
        }
    }

    private bool MatchesDetails(Order order, OrderType orderType, OrderDetails requestedDetails)
    {
        var existingDetails = DeserializeDetails(order.OrderDetailsJson);

        switch (orderType)
        {
            case OrderType.BillPayment:
            {
                var request = requestedDetails.BillPayment;
                var existing = existingDetails.BillPayment;
                if (request == null || existing == null)
                {
                    return false;
                }

                return existing.BillerId == request.BillerId
                    && string.Equals(
                        NormalizeReference(existing.BillReference),
                        NormalizeReference(request.BillReference),
                        StringComparison.OrdinalIgnoreCase);
            }
            case OrderType.BankTransfer:
            {
                var request = requestedDetails.BankTransfer;
                var existing = existingDetails.BankTransfer;
                if (request == null || existing == null)
                {
                    return false;
                }

                if (request.DestinationAccountId.HasValue)
                {
                    return existing.DestinationAccountId == request.DestinationAccountId;
                }

                return string.Equals(
                    NormalizeReference(existing.DestinationAccountNumber),
                    NormalizeReference(request.DestinationAccountNumber),
                    StringComparison.OrdinalIgnoreCase);
            }
            case OrderType.CashCollection:
            {
                var request = requestedDetails.CashCollection;
                var existing = existingDetails.CashCollection;
                if (request == null || existing == null)
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(request.PickupToken))
                {
                    return string.Equals(
                        NormalizeReference(existing.PickupToken),
                        NormalizeReference(request.PickupToken),
                        StringComparison.OrdinalIgnoreCase);
                }

                return existing.RecipientId == request.RecipientId;
            }
            default:
                return false;
        }
    }

    private List<OrderPartyRole> BuildPartyRoles(
        Guid orderId,
        Guid tenantId,
        CreateOrderRequest request,
        OrderType orderType)
    {
        var payerRef = request.Payer ?? new PartyRef(request.CustomerId, null, null);
        var partyRoles = new List<OrderPartyRole>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OrderId = orderId,
                PartyId = payerRef.PartyId,
                Role = PayerRole,
                DetailsJson = _jsonSerializer.Serialize(payerRef)
            }
        };

        var payeeRef = request.Payee ?? ResolvePayeeReference(orderType, request);
        if (payeeRef != null)
        {
            partyRoles.Add(new OrderPartyRole
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OrderId = orderId,
                PartyId = payeeRef.PartyId,
                Role = PayeeRole,
                DetailsJson = _jsonSerializer.Serialize(payeeRef)
            });
        }

        return partyRoles;
    }

    private static PartyRef? ResolvePayeeReference(OrderType orderType, CreateOrderRequest request)
    {
        return orderType switch
        {
            OrderType.BillPayment when request.Details.BillPayment != null
                => new PartyRef(request.Details.BillPayment.BillerId, null, request.Details.BillPayment.BillReference),
            OrderType.CashCollection when request.Details.CashCollection != null
                => new PartyRef(request.Details.CashCollection.RecipientId, null, request.Details.CashCollection.PickupToken),
            _ => null
        };
    }

    private List<OrderItem> BuildOrderItems(
        Guid orderId,
        Guid tenantId,
        IReadOnlyCollection<OrderItemRequest>? items)
    {
        if (items == null || items.Count == 0)
        {
            return new List<OrderItem>();
        }

        return items.Select(item => new OrderItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = orderId,
            ItemType = item.ItemType.Trim(),
            Reference = item.Reference.Trim(),
            Amount = item.Amount,
            Currency = NormalizeCurrency(item.Currency),
            MetadataJson = _jsonSerializer.Serialize(item.Metadata ?? new Dictionary<string, string>())
        }).ToList();
    }

    private Invoice BuildInvoice(
        Guid orderId,
        string orderNumber,
        Guid tenantId,
        CreateOrderRequest request,
        IReadOnlyCollection<OrderItem> orderItems)
    {
        var issueDate = DateTime.UtcNow;
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = orderId,
            CustomerAccountId = request.CustomerId,
            IssueDate = issueDate,
            DueDate = issueDate,
            Currency = NormalizeCurrency(request.Currency),
            Status = "Issued",
            ProvenanceJson = "{}",
            Subtotal = 0,
            TaxTotal = 0,
            DiscountTotal = 0,
            Total = 0,
            Lines = new List<InvoiceLine>()
        };

        if (orderItems.Count > 0)
        {
            foreach (var item in orderItems)
            {
                invoice.Lines.Add(new InvoiceLine
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    InvoiceId = invoice.Id,
                    Description = $"{item.ItemType}: {item.Reference}",
                    Quantity = 1,
                    UnitPrice = item.Amount,
                    TaxRate = 0,
                    LineTotal = item.Amount,
                    MetadataJson = item.MetadataJson
                });
            }
        }
        else
        {
            invoice.Lines.Add(new InvoiceLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                InvoiceId = invoice.Id,
                Description = $"Order {orderNumber}",
                Quantity = 1,
                UnitPrice = request.Amount,
                TaxRate = 0,
                LineTotal = request.Amount,
                MetadataJson = "{}"
            });
        }

        invoice.Subtotal = invoice.Lines.Sum(line => line.LineTotal);
        invoice.Total = invoice.Subtotal + invoice.TaxTotal - invoice.DiscountTotal;

        return invoice;
    }

    private OrderDetailResponse BuildDetailResponse(
        Order order,
        IReadOnlyCollection<OrderItem> items,
        Guid? paymentIntentId,
        string? paymentStatus,
        string? invoiceStatus,
        Guid? payoutId,
        string? payoutStatus)
    {
        var pricingSnapshot = DeserializePricing(order.ProvenanceJson);
        var feesSnapshot = DeserializeFees(order.FeesJson)
            ?? new OrderFeeSnapshot(pricingSnapshot?.FeesTotal, Array.Empty<FeeBreakdownItem>());
        var details = DeserializeDetails(order.OrderDetailsJson);

        return new OrderDetailResponse(
            order.Id,
            order.TenantId,
            order.OrderNumber,
            order.InvoiceId,
            order.Status,
            paymentStatus,
            invoiceStatus,
            order.OrderType,
            order.ServiceCode,
            details,
            items.Select(item => new OrderItemResponse(
                item.Id,
                item.ItemType,
                item.Reference,
                item.Amount,
                item.Currency,
                DeserializeMetadata(item.MetadataJson))).ToList(),
            new OrderAmountSnapshot(
                order.AmountIn,
                order.CurrencyIn,
                pricingSnapshot?.TotalAmount ?? order.AmountOut),
            feesSnapshot,
            new OrderFxSnapshot(pricingSnapshot?.ExchangeRate, pricingSnapshot?.RateMarkup),
            paymentIntentId,
            payoutId,
            payoutStatus,
            null,
            order.CreatedAt);
    }

    private OrderSummaryResponse BuildSummaryResponse(Order order, string? paymentStatus, string? invoiceStatus)
    {
        var pricingSnapshot = DeserializePricing(order.ProvenanceJson);
        var details = DeserializeDetails(order.OrderDetailsJson);

        return new OrderSummaryResponse(
            order.Id,
            order.TenantId,
            order.OrderNumber,
            order.InvoiceId,
            order.Status,
            paymentStatus,
            invoiceStatus,
            order.OrderType,
            order.ServiceCode,
            details,
            new OrderAmountSnapshot(
                order.AmountIn,
                order.CurrencyIn,
                pricingSnapshot?.TotalAmount ?? order.AmountOut),
            order.CreatedAt);
    }

    private OrderDetails DeserializeDetails(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return EmptyDetails;
        }

        return _jsonSerializer.Deserialize<OrderDetails>(json) ?? EmptyDetails;
    }

    private OrderFeeSnapshot? DeserializeFees(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return _jsonSerializer.Deserialize<OrderFeeSnapshot>(json);
    }

    private OrderPricingSnapshot? DeserializePricing(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return _jsonSerializer.Deserialize<OrderPricingSnapshot>(json);
    }

    private IReadOnlyDictionary<string, string>? DeserializeMetadata(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return _jsonSerializer.Deserialize<Dictionary<string, string>>(json);
    }

    private static string NormalizeReference(string? reference)
        => string.IsNullOrWhiteSpace(reference) ? string.Empty : reference.Trim();

    private async Task EnsurePermissionAsync(string permissionKey, CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        var hasPermission = await _permissionService.HasPermissionAsync(userId.Value, permissionKey, cancellationToken);
        if (!hasPermission)
        {
            throw new InvalidOperationException($"Permission {permissionKey} is required.");
        }
    }

    private sealed record OrderPricingSnapshot(
        Guid PricingQuoteId,
        decimal? ExchangeRate,
        decimal? RateMarkup,
        decimal? FeesTotal,
        decimal? TotalAmount);
}
