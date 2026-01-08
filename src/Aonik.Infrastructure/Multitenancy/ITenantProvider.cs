namespace Aonik.Infrastructure.Multitenancy;

/// <summary>
/// Provides access to the current tenant context.
/// </summary>
public interface ITenantProvider
{
    /// <summary>
    /// Gets the current tenant identifier.
    /// </summary>
    /// <returns>The tenant ID for the current request context.</returns>
    /// <exception cref="InvalidOperationException">Thrown when tenant context is not available.</exception>
    Guid GetCurrentTenantId();

    /// <summary>
    /// Tries to get the current tenant identifier.
    /// </summary>
    /// <param name="tenantId">The tenant ID if available.</param>
    /// <returns>True if tenant context is available, false otherwise.</returns>
    bool TryGetCurrentTenantId(out Guid tenantId);
}
