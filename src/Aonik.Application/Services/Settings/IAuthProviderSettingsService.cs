using Aonik.Application.Models.Settings;

namespace Aonik.Application.Services.Settings;

public interface IAuthProviderSettingsService
{
    Task<AuthProviderSettingsSnapshot> GetAsync(CancellationToken cancellationToken = default);
    Task<AuthProviderSettingsSnapshot> UpdateAsync(AuthProviderSettingsUpdate update, CancellationToken cancellationToken = default);
}
