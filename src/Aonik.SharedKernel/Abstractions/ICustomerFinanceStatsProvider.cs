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
public record CustomerFinanceStats(
    int TotalOrders,
    IReadOnlyList<CustomerFinanceCurrencyAmount> TotalPaidByCurrency,
    IReadOnlyList<CustomerFinanceCurrencyAmount> OutstandingByCurrency,
    DateTime? LastActivityAt
);

/// <summary>
/// An amount in a specific currency, used in cross-module financial stats.
/// </summary>
public record CustomerFinanceCurrencyAmount(
    string Currency,
    decimal Amount
);
