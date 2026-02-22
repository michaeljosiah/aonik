namespace Aonik.SharedKernel.Abstractions.Multitenancy;

/// <summary>
/// Provides the and current tenant information allows changing the tenant context.
/// </summary>
public interface ICurrentTenant
{
    /// <summary>
    /// Gets the current tenant ID.
    /// </summary>
    Guid? Id { get; }

    /// <summary>
    /// Gets the current tenant name.
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// Gets whether a tenant is currently set.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Temporarily changes the current tenant to the specified tenant.
    /// </summary>
    /// <param name="tenantId">The tenant ID to set, or null to reset.</param>
    /// <returns>An <see cref="IDisposable"/> that restores the previous tenant when disposed.</returns>
    IDisposable Change(Guid? tenantId);
}
