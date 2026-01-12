namespace Aonik.Application.Abstractions.Settings;

using Aonik.Application.Models.Settings;
using Aonik.Domain.Settings;

public interface ISettingProvider
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task<string> GetRequiredAsync(string key, CancellationToken cancellationToken = default);
    Task<string?> GetForScopeAsync(
        string key,
        SettingScope scope,
        Guid? tenantId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    Task<SettingResolution> GetResolvedAsync(
        string key,
        Guid? tenantId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default);
}
