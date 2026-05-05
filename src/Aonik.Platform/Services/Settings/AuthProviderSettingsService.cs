using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Contracts.Models.Settings;
using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Platform.Services.Settings;

internal class AuthProviderSettingsService : IAuthProviderSettingsService
{
    private readonly ISettingProvider _settingProvider;

    public AuthProviderSettingsService(
        ISettingProvider settingProvider)
    {
        _settingProvider = settingProvider;
    }

    public async Task<AuthProviderSettingsSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        var activeProvider = await _settingProvider.GetAsync(AuthSettingNames.Provider, cancellationToken)
                             ?? "AzureAd";

        var auth0ManagementClientSecret = await _settingProvider.GetAsync(AuthSettingNames.Auth0ManagementClientSecret, cancellationToken);
        var azureClientSecret = await _settingProvider.GetAsync(AuthSettingNames.AzureAdClientSecret, cancellationToken);

        return new AuthProviderSettingsSnapshot(
            activeProvider,
            new Auth0SettingsSnapshot(
                await _settingProvider.GetAsync(AuthSettingNames.Auth0Domain, cancellationToken),
                await _settingProvider.GetAsync(AuthSettingNames.Auth0Audience, cancellationToken),
                await _settingProvider.GetAsync(AuthSettingNames.Auth0ClientId, cancellationToken),
                await _settingProvider.GetAsync(AuthSettingNames.Auth0ManagementClientId, cancellationToken),
                !string.IsNullOrWhiteSpace(auth0ManagementClientSecret),
                await _settingProvider.GetAsync(AuthSettingNames.Auth0Connection, cancellationToken),
                await _settingProvider.GetAsync(AuthSettingNames.Auth0ManagementAudience, cancellationToken)),
            new AzureAdSettingsSnapshot(
                await _settingProvider.GetAsync(AuthSettingNames.AzureAdAuthority, cancellationToken),
                await _settingProvider.GetAsync(AuthSettingNames.AzureAdAudience, cancellationToken),
                await _settingProvider.GetAsync(AuthSettingNames.AzureAdClientId, cancellationToken),
                !string.IsNullOrWhiteSpace(azureClientSecret),
                await _settingProvider.GetAsync(AuthSettingNames.AzureAdTenantId, cancellationToken),
                await _settingProvider.GetAsync(AuthSettingNames.AzureAdUpnDomain, cancellationToken)));
    }

    public async Task<AuthProviderSettingsSnapshot> UpdateAsync(
        AuthProviderSettingsUpdate update,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            "Authentication provider settings are configuration-managed. Update appsettings/environment variables and redeploy.");
    }
}
