using Aonik.Platform.Contracts.Models.Settings;

namespace Aonik.Platform.Contracts.Services.Settings;

/// <summary>
/// Read-only viewer onto the outbound messaging configuration. Mirrors
/// <see cref="IAuthProviderSettingsService"/> — the snapshot reports
/// which provider is active and whether each channel's credentials are
/// set, but updates throw because all of this configuration is
/// environment-managed at the moment (see appsettings.json /
/// Communication:* keys).
/// </summary>
public interface ICommunicationProviderSettingsService
{
    Task<CommunicationProviderSettingsSnapshot> GetAsync(CancellationToken cancellationToken = default);

    Task<CommunicationProviderSettingsSnapshot> UpdateAsync(
        CommunicationProviderSettingsUpdate update,
        CancellationToken cancellationToken = default);
}
