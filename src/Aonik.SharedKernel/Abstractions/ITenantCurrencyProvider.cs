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

    /// <summary>
    /// The tenant's canonical default currency (Tenant.DefaultCurrency), uppercased, or null when
    /// the tenant record carries none. Spec 070 §9: consumers label amounts with this rather than
    /// keeping a parallel setting that goes stale the day the tenant's currency changes.
    /// </summary>
    Task<string?> GetTenantDefaultCurrencyAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
