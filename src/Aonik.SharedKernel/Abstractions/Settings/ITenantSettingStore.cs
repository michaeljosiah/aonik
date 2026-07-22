namespace Aonik.SharedKernel.Abstractions.Settings;

/// <summary>
/// Tenant-scoped read/write access to the settings store for a module that OWNS the keys it
/// touches (Spec 070's <c>Commerce.Storefront.*</c>). Lives on SharedKernel for the same reason as
/// <see cref="ISettingProvider"/>: no back-pointing reference on the Platform implementation.
///
/// <para><strong>The caller owns authorization.</strong> This deliberately bypasses the platform
/// <c>Settings.Read</c>/<c>Settings.Write</c> permission checks: module endpoints gate access with
/// their own module-appropriate policy — the whole reason Spec 070 §9 gives storefront settings a
/// Commerce endpoint is that an Operations user can edit every product yet holds no platform
/// Settings permission. A module must only ever touch keys it owns; this is not a general-purpose
/// settings API, and <see cref="ISettingProvider"/> remains the read surface for everything else.</para>
/// </summary>
public interface ITenantSettingStore
{
    /// <summary>The tenant-scoped stored value for <paramref name="key"/>, or null when the tenant
    /// has no override. No fallback — callers compose their own Global/default chain.</summary>
    Task<string?> GetTenantValueAsync(string key, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Writes a tenant-scoped value. Null (or whitespace) clears the tenant override so
    /// resolution falls back through Global → configuration → registered default.</summary>
    Task SetTenantValueAsync(string key, string? value, Guid tenantId, CancellationToken cancellationToken = default);
}
