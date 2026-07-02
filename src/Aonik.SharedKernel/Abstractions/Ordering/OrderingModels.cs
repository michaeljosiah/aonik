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

/// <summary>An explicit party role to persist on the order (Spec 053 §10/§11) — e.g. the
/// <c>Supplier</c> counterparty on a purchase order. <see cref="Role"/> uses the
/// <see cref="OrderPartyRoleCodes"/> known values (an open string, so new roles are additive).</summary>
public sealed record OrderPartyRoleCommand(Guid PartyId, string Role);

/// <summary>Create any order type. <see cref="AmountIn"/> defaults to the sum of the line
/// <c>AmountIn</c> values when null. <see cref="PartyRoles"/> optionally supplies additional
/// party roles to materialize alongside the auto-materialized Payer (from
/// <see cref="PayerPartyId"/>) and per-line Receiver roles; entries duplicating those (same
/// party + role) are deduped (Spec 053 §10 — additive, existing callers unaffected).</summary>
public sealed record CreateOrderCommand(
    string OrderType,
    Guid? PayerPartyId,
    string CurrencyIn,
    IReadOnlyList<OrderItemCommand> Items,
    string? IdempotencyKey = null,
    string? ProvenanceJson = null,
    decimal? AmountIn = null,
    IReadOnlyList<OrderPartyRoleCommand>? PartyRoles = null);

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

/// <summary>List filter. The created-range bounds (Spec 055 §9 — additive, like the Spec 053/054
/// contract extensions) are half-open over the order's <c>CreatedAt</c> UTC instant:
/// <see cref="CreatedFromUtc"/> is inclusive, <see cref="CreatedToUtc"/> exclusive, so adjacent
/// windows never double-count a boundary order.</summary>
public sealed record ListOrdersQuery(
    string? OrderType = null,
    string? Status = null,
    Guid? PayerPartyId = null,
    int PageNumber = 1,
    int PageSize = 20,
    DateTime? CreatedFromUtc = null,
    DateTime? CreatedToUtc = null);

/// <summary>Links an order to the execution record that fulfils it. Exactly one id must be set,
/// matching the <c>OrderFulfilmentRef</c> "one-of" CHECK.</summary>
public sealed record OrderFulfilmentLink(
    Guid? PayoutId = null,
    Guid? PaymentIntentId = null,
    Guid? PartnerBillPaymentId = null);
