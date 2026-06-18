namespace Aonik.SharedKernel.Abstractions.Ordering;

// Spec 041 / ADR-011 — the core Ordering contract surface. These DTOs/commands are the
// type-agnostic shape of an order: a header, line items (financial or retail), parties, funding,
// and fulfilment. Type-specific creation (bill payment with FX/compliance, product purchase with
// inventory) composes IOrderService for persistence and lifecycle, adding its own pre/post steps.
// Records carry only what consumers need — never the Finance entities.

/// <summary>One line of an order. Retail fields (<see cref="Quantity"/>, <see cref="UnitPrice"/>,
/// <see cref="ProductId"/>, <see cref="Sku"/>) are populated only for product-purchase lines;
/// the line total is carried by <see cref="AmountIn"/>.</summary>
public sealed record OrderItemCommand(
    string ItemType,
    int ItemIndex,
    decimal AmountIn,
    string CurrencyIn,
    Guid? ReceiverPartyId = null,
    decimal? Quantity = null,
    decimal? UnitPrice = null,
    Guid? ProductId = null,
    string? Sku = null,
    string? DetailsJson = null);

/// <summary>Create any order type. <see cref="AmountIn"/> defaults to the sum of the line
/// <c>AmountIn</c> values when null.</summary>
public sealed record CreateOrderCommand(
    string OrderType,
    Guid? PayerPartyId,
    string CurrencyIn,
    IReadOnlyList<OrderItemCommand> Items,
    string? IdempotencyKey = null,
    string? ProvenanceJson = null,
    decimal? AmountIn = null);

public sealed record OrderItemDto(
    Guid Id,
    string ItemType,
    int ItemIndex,
    string Status,
    decimal AmountIn,
    string CurrencyIn,
    Guid? ReceiverPartyId,
    decimal? Quantity,
    decimal? UnitPrice,
    Guid? ProductId,
    string? Sku,
    string DetailsJson);

public sealed record OrderDto(
    Guid Id,
    Guid TenantId,
    string OrderType,
    string Status,
    Guid? PayerPartyId,
    decimal AmountIn,
    string CurrencyIn,
    DateTime CreatedAt,
    IReadOnlyList<OrderItemDto> Items);

public sealed record OrderSummary(
    Guid Id,
    string OrderType,
    string Status,
    decimal AmountIn,
    string CurrencyIn,
    DateTime CreatedAt,
    int ItemCount);

public sealed record ListOrdersQuery(
    string? OrderType = null,
    string? Status = null,
    Guid? PayerPartyId = null,
    int PageNumber = 1,
    int PageSize = 20);

/// <summary>Links an order to the execution record that fulfils it. Exactly one id must be set,
/// matching the <c>OrderFulfilmentRef</c> "one-of" CHECK.</summary>
public sealed record OrderFulfilmentLink(
    Guid? PayoutId = null,
    Guid? PaymentIntentId = null,
    Guid? PartnerBillPaymentId = null);
