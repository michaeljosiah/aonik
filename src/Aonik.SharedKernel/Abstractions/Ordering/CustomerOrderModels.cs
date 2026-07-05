namespace Aonik.SharedKernel.Abstractions.Ordering;

// Customer-facing order projections for ICustomerOrderService. These carry only what a
// personal-finance assistant shows an order's owner — compact summaries, never the full Finance
// Order entity or the large BillPaymentOrderResponse (items, service fields, pricing snapshots,
// party roles, history). Kept intentionally in lock-step with the "Simi" order tools' output so
// the assistant's answers stay summary-oriented.

/// <summary>Compact summary of one of the party's orders (list rows).</summary>
public sealed record CustomerOrderSummary(
    Guid OrderId,
    string OrderType,
    string Status,
    string OriginCurrency,
    decimal TotalAmountIn,
    string? DestinationCurrency,
    decimal? TotalAmountOut,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Single-order detail: the header plus the flattened primary line (top receiver / biller
/// and item count) — enough to answer "what happened to my transfer to X" without dumping the full
/// item list.</summary>
public sealed record CustomerOrderDetail(
    Guid OrderId,
    string OrderType,
    string Status,
    string OriginCountry,
    string OriginCurrency,
    decimal TotalAmountIn,
    decimal TotalFeesAmount,
    decimal TotalAmountOut,
    string? DestinationCurrency,
    string? PurposeCode,
    int ItemCount,
    string? PrimaryReceiverName,
    string? PrimaryBillerName,
    DateTime CreatedAt,
    DateTime? SubmittedAt);
