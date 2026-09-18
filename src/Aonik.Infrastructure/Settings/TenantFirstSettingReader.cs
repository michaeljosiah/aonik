using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Infrastructure.Settings;

/// <summary>
/// One rule for a setting a tenant may hold as their own: the tenant's value when the call has a
/// tenant and the tenant has set one, otherwise the platform's resolution of the same key — the
/// global value, then the configuration seed, then the definition's default.
///
/// <para>
/// The tenant read is the module-facing one (<see cref="ITenantSettingStore"/>), which relies on
/// the calling endpoint's policy rather than a <c>Settings.Read</c> grant the acting user may not
/// hold — a parent asking for a chapter is not a settings administrator. Nothing is read here at
/// construction; every consumer asks at the moment it needs the value.
/// </para>
/// </summary>
internal sealed class TenantFirstSettingReader
{
    private readonly ITenantSettingStore _tenantSettings;
    private readonly ISettingProvider _settings;
    private readonly ITenantProvider _tenantProvider;

    public TenantFirstSettingReader(ITenantSettingStore tenantSettings, ISettingProvider settings, ITenantProvider tenantProvider)
    {
        _tenantSettings = tenantSettings;
        _settings = settings;
        _tenantProvider = tenantProvider;
    }

    public async Task<string?> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_tenantProvider.TryGetCurrentTenantId(out var tenantId) && tenantId != Guid.Empty)
        {
            var tenantValue = await _tenantSettings.GetTenantValueAsync(key, tenantId, cancellationToken);

            if (!string.IsNullOrWhiteSpace(tenantValue))
            {
                return tenantValue;
            }
        }

        var value = await _settings.GetAsync(key, cancellationToken);

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
