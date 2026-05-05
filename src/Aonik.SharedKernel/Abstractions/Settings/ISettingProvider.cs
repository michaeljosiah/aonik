namespace Aonik.SharedKernel.Abstractions.Settings;

/// <summary>
/// Read-side abstraction for the platform's hierarchical settings store.
/// Lives on SharedKernel so any module (Ai, Infrastructure, Finance) can
/// read settings without taking a back-pointing reference on the
/// Platform implementation. Platform's <c>SettingProvider</c> is the
/// only implementation; it owns caching, encryption, and the EF Core
/// query that walks the User → Tenant → Global resolution chain.
/// </summary>
public interface ISettingProvider
{
    /// <summary>
    /// Returns the resolved value for <paramref name="key"/> in the
    /// current ambient scope (caller's user or tenant), falling back
    /// through the User → Tenant → Global chain. Returns <c>null</c> if
    /// the key has no value at any scope.
    /// </summary>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Same as <see cref="GetAsync"/> but throws when no value resolves —
    /// for required settings whose absence is a startup-time misconfiguration.
    /// </summary>
    Task<string> GetRequiredAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the value at a specific scope (no fallback). Pass an explicit
    /// <paramref name="tenantId"/> / <paramref name="userId"/> when reading
    /// outside the ambient request scope (e.g. background jobs querying a
    /// specific tenant's row). Returns <c>null</c> when no row matches.
    /// </summary>
    Task<string?> GetForScopeAsync(
        string key,
        SettingScope scope,
        Guid? tenantId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the value AND reports which scope it came from, so callers
    /// can show "tenant override" / "global default" / etc.
    /// </summary>
    Task<SettingResolution> GetResolvedAsync(
        string key,
        Guid? tenantId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default);
}
