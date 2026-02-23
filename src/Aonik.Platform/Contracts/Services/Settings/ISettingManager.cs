namespace Aonik.Platform.Contracts.Services.Settings;

using Aonik.Platform.Entities.Settings;

public interface ISettingManager
{
    Task SetAsync(string key, string? value, CancellationToken cancellationToken = default);
    Task SetAsync(
        string key,
        string? value,
        SettingScope scope,
        Guid? tenantId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default);
    Task<bool> HasStoredValueAsync(
        string key,
        SettingScope scope,
        Guid? tenantId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default);
}
