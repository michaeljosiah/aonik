namespace Aonik.SharedKernel.Abstractions.Ordering;

/// <summary>
/// Cross-module projection of an order. Carries only what PersonalFinance and other
/// non-Finance consumers actually read. Mirrors the dual-currency shape of
/// <c>Aonik.Finance.Entities.Orders.Order</c> (AmountIn/CurrencyIn for the requested
/// service amount; AmountOut/CurrencyOut where the order involves an FX conversion).
/// </summary>
public sealed record OrderHistoryItem(
    Guid OrderId,
    string OrderType,
    string Status,
    decimal AmountIn,
    string CurrencyIn,
    decimal? AmountOut,
    string? CurrencyOut,
    DateTime CreatedAt);
