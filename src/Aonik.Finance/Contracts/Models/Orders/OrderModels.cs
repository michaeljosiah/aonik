namespace Aonik.Finance.Contracts.Models.Orders;

public record CreateBillPaymentOrderRequest(
    Guid PayerPartyId,
    string OriginCountry,
    string OriginCurrency,
    string? PurposeCode,
    string? Notes,
    string? IdempotencyKey,
    List<CreateBillPaymentItemRequest>? Items);

public record CreateBillPaymentItemRequest(
    Guid BillerId,
    Guid ServiceId,
    string ServiceCode,
    Dictionary<string, string> ServiceFieldValues,
    Guid? ReceiverPartyId,
    CreateReceiverRequest? NewReceiver,
    string? RelationshipTypeCode,
    decimal? OriginAmount,
    decimal? DestinationAmount,
    string DestinationCurrency,
    string DestinationCountry,
    Guid PricingQuoteId,
    string? PurposeCode,
    string? Notes);

public record CreateReceiverRequest(
    string DisplayName,
    string PartyType,
    string? FirstName,
    string? LastName,
    string? Phone,
    string? Email,
    string? CountryCode);

public record UpdateBillPaymentItemRequest(
    Dictionary<string, string>? ServiceFieldValues,
    Guid? ReceiverPartyId,
    string? RelationshipTypeCode,
    decimal? OriginAmount,
    decimal? DestinationAmount,
    Guid? PricingQuoteId,
    string? PurposeCode,
    string? Notes);

public record BillPaymentOrderResponse(
    Guid OrderId,
    string OrderType,
    string Status,
    Guid PayerPartyId,
    string PayerName,
    string OriginCountry,
    string OriginCurrency,
    decimal TotalAmountIn,
    decimal TotalFeesAmount,
    decimal TotalAmountOut,
    string? DestinationCurrency,
    string? PurposeCode,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    IReadOnlyList<OrderItemResponse> Items);

public record OrderItemResponse(
    Guid OrderItemId,
    int ItemIndex,
    string ItemType,
    string Status,
    Guid BillerId,
    string BillerName,
    Guid ServiceId,
    string ServiceCode,
    string ServiceName,
    Dictionary<string, string> ServiceFieldValues,
    Guid ReceiverPartyId,
    string ReceiverName,
    string? RelationshipTypeCode,
    decimal AmountIn,
    string CurrencyIn,
    decimal AmountOut,
    string CurrencyOut,
    decimal FeesTotal,
    decimal ExchangeRate,
    Guid? PricingQuoteId,
    DateTime? QuoteExpiresAt,
    bool IsQuoteExpired);
