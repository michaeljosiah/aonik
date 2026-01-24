using Aonik.Api.Contracts.Pricing;

namespace Aonik.Api.Contracts.Orders;

public record ValidateDuplicateOrderRequest(
    Guid CustomerId,
    string OrderType,
    string ServiceCode,
    decimal Amount,
    string Currency,
    OrderDetails Details,
    DateTimeOffset? RequestedAt);

public record ValidateDuplicateOrderResponse(
    Guid? OrderId,
    Guid? TenantId,
    string? OrderNumber,
    Guid? InvoiceId,
    string? Status,
    DateTime? CreatedAt);

public record CreateOrderRequest(
    Guid CustomerId,
    string OrderType,
    string ServiceCode,
    decimal Amount,
    string Currency,
    Guid PricingQuoteId,
    decimal? ExchangeRate,
    decimal? RateMarkup,
    decimal? FeesTotal,
    decimal? TotalAmount,
    IReadOnlyCollection<FeeBreakdownItem>? FeeBreakdown,
    PartyRef? Payer,
    PartyRef? Payee,
    OrderDetails Details,
    IReadOnlyCollection<OrderItemRequest>? Items,
    IReadOnlyDictionary<string, string>? Metadata);

public record CreateOrderResponse(
    Guid OrderId,
    Guid TenantId,
    string OrderNumber,
    Guid? InvoiceId,
    string Status,
    DateTime CreatedAt,
    string? PaymentStatus,
    string? InvoiceStatus);

public record OrderDetailResponse(
    Guid OrderId,
    Guid TenantId,
    string OrderNumber,
    Guid? InvoiceId,
    string Status,
    string? PaymentStatus,
    string? InvoiceStatus,
    string OrderType,
    string ServiceCode,
    OrderDetails Details,
    IReadOnlyCollection<OrderItemResponse> Items,
    OrderAmountSnapshot Amounts,
    OrderFeeSnapshot Fees,
    OrderFxSnapshot Fx,
    Guid? PaymentIntentId,
    Guid? PayoutId,
    string? PayoutStatus,
    LedgerReference? LedgerReference,
    DateTime CreatedAt);

public record OrderSummaryResponse(
    Guid OrderId,
    Guid TenantId,
    string OrderNumber,
    Guid? InvoiceId,
    string Status,
    string? PaymentStatus,
    string? InvoiceStatus,
    string OrderType,
    string ServiceCode,
    OrderDetails Details,
    OrderAmountSnapshot Amounts,
    DateTime CreatedAt);

public record OrderListResponse(
    IReadOnlyCollection<OrderSummaryResponse> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);

public record OrderListRequest(
    Guid? CustomerId,
    string? Status,
    string? OrderType,
    string? ServiceCode,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    int PageNumber,
    int PageSize);

public record OrderDetails(
    BillPaymentDetails? BillPayment,
    BankTransferDetails? BankTransfer,
    CashCollectionDetails? CashCollection);

public record OrderItemRequest(
    string ItemType,
    string Reference,
    decimal Amount,
    string Currency,
    IReadOnlyDictionary<string, string>? Metadata);

public record OrderItemResponse(
    Guid OrderItemId,
    string ItemType,
    string Reference,
    decimal Amount,
    string Currency,
    IReadOnlyDictionary<string, string>? Metadata);

public record PartyRef(
    Guid PartyId,
    string? DisplayName,
    string? Reference);

public record BillPaymentDetails(
    Guid BillerId,
    string BillReference,
    Guid? BillerAccountId,
    string? BillerCategory,
    string? BillerCountry);

public record BankTransferDetails(
    Guid? DestinationAccountId,
    string DestinationAccountNumber,
    string DestinationBankCode,
    string DestinationCountry,
    string? Purpose);

public record CashCollectionDetails(
    Guid RecipientId,
    string? PickupLocation,
    string? PickupToken,
    Guid? SenderId);

public record OrderAmountSnapshot(
    decimal Amount,
    string Currency,
    decimal? TotalAmount);

public record OrderFeeSnapshot(
    decimal? FeesTotal,
    IReadOnlyCollection<FeeBreakdownItem>? FeeBreakdown);

public record OrderFxSnapshot(
    decimal? ExchangeRate,
    decimal? RateMarkup);

public record LedgerReference(
    Guid? JournalId,
    IReadOnlyCollection<Guid>? EntryIds);
