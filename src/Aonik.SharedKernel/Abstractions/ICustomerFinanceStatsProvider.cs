namespace Aonik.SharedKernel.Abstractions;

/// <summary>
/// Cross-module contract for retrieving customer-level financial statistics.
/// Implemented by the Finance module; consumed by Platform services
/// (e.g., CustomerAdminService) without requiring a direct dependency
/// on Finance entities or DbContext.
/// </summary>
public interface ICustomerFinanceStatsProvider
{
    /// <summary>
    /// Retrieves financial statistics for a customer (party) in the current tenant.
    /// </summary>
    Task<CustomerFinanceStats> GetStatsAsync(Guid tenantId, Guid partyId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Financial statistics for a customer, returned by <see cref="ICustomerFinanceStatsProvider"/>.
/// </summary>
/// <param name="TotalOrders">Lifetime distinct order count.</param>
/// <param name="TotalPaidByCurrency">Lifetime total of captured payments,
/// grouped by currency. Equivalent to LTV.</param>
/// <param name="OutstandingByCurrency">Sum of <c>AmountIn</c> on
/// non-terminal orders, grouped by currency.</param>
/// <param name="LastActivityAt">Most recent order update or payment
/// capture, whichever is later.</param>
/// <param name="OpenOrderCount">Count of orders not yet in a terminal
/// status (Complete / Cancelled / Failed / Expired).</param>
/// <param name="TrailingTwelveMonthsByCurrency">Captured payments in the
/// trailing 12 months, grouped by currency. The closest analogue we have
/// to ARR for a non-subscription order model.</param>
/// <param name="TrailingThirtyDaysByCurrency">Captured payments in the
/// trailing 30 days, grouped by currency. Reads as a rough monthly run
/// rate; jittery for low-volume customers.</param>
public record CustomerFinanceStats(
    int TotalOrders,
    IReadOnlyList<CustomerFinanceCurrencyAmount> TotalPaidByCurrency,
    IReadOnlyList<CustomerFinanceCurrencyAmount> OutstandingByCurrency,
    DateTime? LastActivityAt,
    int OpenOrderCount,
    IReadOnlyList<CustomerFinanceCurrencyAmount> TrailingTwelveMonthsByCurrency,
    IReadOnlyList<CustomerFinanceCurrencyAmount> TrailingThirtyDaysByCurrency
);

/// <summary>
/// An amount in a specific currency, used in cross-module financial stats.
/// </summary>
public record CustomerFinanceCurrencyAmount(
    string Currency,
    decimal Amount
);
