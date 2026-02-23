using Aonik.Platform.Contracts.Models.Settings;

namespace Aonik.Platform.Contracts.Services.Settings;

public interface IAuthProviderSettingsService
{
    Task<AuthProviderSettingsSnapshot> GetAsync(CancellationToken cancellationToken = default);
    Task<AuthProviderSettingsSnapshot> UpdateAsync(AuthProviderSettingsUpdate update, CancellationToken cancellationToken = default);
}
