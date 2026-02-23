namespace Aonik.Platform.Contracts.Services.Settings;

using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Entities.Settings;

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
