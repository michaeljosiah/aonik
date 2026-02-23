namespace Aonik.SharedKernel.Abstractions;

/// <summary>
/// Provides tenant currency configuration. Implemented by Platform, consumed by Finance.
/// </summary>
public interface ITenantCurrencyProvider
{
    /// <summary>
    /// Gets the list of active currency codes configured for a tenant.
    /// Falls back to the tenant's default currency, or ["USD"] if none configured.
    /// </summary>
    Task<List<string>> GetTenantCurrencyCodesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
