using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Infrastructure.Tests.Authentication;

/// <summary>
/// Spec 029 — minimal ISettingProvider stub for unit-testing the Keycloak*
/// services. Tests preload the secrets / authority via <see cref="Set"/>;
/// the system-under-test reads through GetAsync / GetRequiredAsync as it
/// would against the real settings store. Per-scope lookups are not used
/// by any of the Keycloak* services, so those interface methods are
/// implemented as simple ambient-scope passthroughs.
/// </summary>
internal sealed class InMemorySettingProvider : ISettingProvider
{
    private readonly ConcurrentDictionary<string, string> _values = new();

    public void Set(string key, string value) => _values[key] = value;

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);

    public Task<string> GetRequiredAsync(string key, CancellationToken cancellationToken = default)
        => _values.TryGetValue(key, out var value)
            ? Task.FromResult(value)
            : throw new InvalidOperationException($"Setting '{key}' is required but missing.");

    public Task<string?> GetForScopeAsync(
        string key,
        SettingScope scope,
        Guid? tenantId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
        => GetAsync(key, cancellationToken);

    public Task<SettingResolution> GetResolvedAsync(
        string key,
        Guid? tenantId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var value = _values.TryGetValue(key, out var v) ? v : null;
        return Task.FromResult(new SettingResolution(key, value, "Global"));
    }
}
