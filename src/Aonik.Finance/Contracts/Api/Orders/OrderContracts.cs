namespace Aonik.Finance.Contracts.Api.Orders;

public record CreateBillPaymentOrderRequest(
    Guid PayerPartyId,
    string OriginCountry,
    string OriginCurrency,
    string? PurposeCode,
    string? Notes,
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

public record CancelOrderRequest(string? Reason);

public record ListOrdersRequest(
    int PageNumber = 1,
    int PageSize = 20,
    string? Status = null,
    string? OrderType = null,
    string? Search = null);

public record OrderListItemResponse(
    Guid OrderId,
    string OrderType,
    string Status,
    Guid? PayerPartyId,
    string PayerName,
    string? OriginCountry,
    string OriginCurrency,
    decimal TotalAmountIn,
    decimal? TotalAmountOut,
    string? DestinationCurrency,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

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
    List<OrderItemResponse> Items);

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

public record CreateGuestBillPaymentDraftRequest(
    Guid BillerId,
    Guid ServiceId,
    string ServiceCode,
    string ServiceName,
    string? BillerName,
    string CountryCode,
    string Currency,
    Dictionary<string, string> ServiceFieldValues,
    bool IsValidated,
    DateTimeOffset CapturedAt,
    string? ValidationMode,
    string? AccountHolderName,
    decimal? RequestedAmount,
    string? Channel);

public record GuestBillPaymentDraftResponse(
    Guid OrderId,
    string Status,
    DateTime CreatedAt);

public record GuestBillPaymentDraftDetailResponse(
    Guid OrderId,
    string Status,
    DateTime CreatedAt,
    string CountryCode,
    string Currency,
    Guid BillerId,
    string? BillerName,
    Guid ServiceId,
    string ServiceCode,
    string ServiceName,
    Dictionary<string, string> ServiceFieldValues,
    bool IsValidated,
    DateTimeOffset CapturedAt,
    string? ValidationMode,
    string? AccountHolderName,
    decimal? RequestedAmount,
    string Channel);
