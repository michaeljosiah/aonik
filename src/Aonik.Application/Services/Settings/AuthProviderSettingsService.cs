using Aonik.Application.Abstractions.Settings;
using Aonik.Application.Models.Settings;
using Aonik.Application.Settings;

namespace Aonik.Application.Services.Settings;

public class AuthProviderSettingsService : IAuthProviderSettingsService
{
    private readonly ISettingProvider _settingProvider;
    private readonly ISettingManager _settingManager;

    public AuthProviderSettingsService(
        ISettingProvider settingProvider,
        ISettingManager settingManager)
    {
        _settingProvider = settingProvider;
        _settingManager = settingManager;
    }

    public async Task<AuthProviderSettingsSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        var activeProvider = await _settingProvider.GetAsync(AuthSettingNames.Provider, cancellationToken)
                             ?? "AzureAd";

        var auth0ClientSecret = await _settingProvider.GetAsync(AuthSettingNames.Auth0ClientSecret, cancellationToken);
        var azureClientSecret = await _settingProvider.GetAsync(AuthSettingNames.AzureAdClientSecret, cancellationToken);

        return new AuthProviderSettingsSnapshot(
            activeProvider,
            new Auth0SettingsSnapshot(
                await _settingProvider.GetAsync(AuthSettingNames.Auth0Domain, cancellationToken),
                await _settingProvider.GetAsync(AuthSettingNames.Auth0Audience, cancellationToken),
                await _settingProvider.GetAsync(AuthSettingNames.Auth0ClientId, cancellationToken),
                !string.IsNullOrWhiteSpace(auth0ClientSecret),
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
        if (!string.Equals(update.ActiveProvider, "AzureAd", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(update.ActiveProvider, "Auth0", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("ActiveProvider must be either 'AzureAd' or 'Auth0'.");
        }

        await _settingManager.SetAsync(AuthSettingNames.Provider, update.ActiveProvider, cancellationToken);

        if (update.Auth0 != null)
        {
            await SetIfNotNullAsync(AuthSettingNames.Auth0Domain, update.Auth0.Domain, cancellationToken);
            await SetIfNotNullAsync(AuthSettingNames.Auth0Audience, update.Auth0.Audience, cancellationToken);
            await SetIfNotNullAsync(AuthSettingNames.Auth0ClientId, update.Auth0.ClientId, cancellationToken);
            await SetIfNotNullAsync(AuthSettingNames.Auth0ClientSecret, update.Auth0.ClientSecret, cancellationToken);
            await SetIfNotNullAsync(AuthSettingNames.Auth0Connection, update.Auth0.Connection, cancellationToken);
            await SetIfNotNullAsync(AuthSettingNames.Auth0ManagementAudience, update.Auth0.ManagementAudience, cancellationToken);
        }

        if (update.AzureAd != null)
        {
            await SetIfNotNullAsync(AuthSettingNames.AzureAdAuthority, update.AzureAd.Authority, cancellationToken);
            await SetIfNotNullAsync(AuthSettingNames.AzureAdAudience, update.AzureAd.Audience, cancellationToken);
            await SetIfNotNullAsync(AuthSettingNames.AzureAdClientId, update.AzureAd.ClientId, cancellationToken);
            await SetIfNotNullAsync(AuthSettingNames.AzureAdClientSecret, update.AzureAd.ClientSecret, cancellationToken);
            await SetIfNotNullAsync(AuthSettingNames.AzureAdTenantId, update.AzureAd.TenantId, cancellationToken);
            await SetIfNotNullAsync(AuthSettingNames.AzureAdUpnDomain, update.AzureAd.UserPrincipalNameDomain, cancellationToken);
        }

        return await GetAsync(cancellationToken);
    }

    private async Task SetIfNotNullAsync(string key, string? value, CancellationToken cancellationToken)
    {
        if (value == null)
        {
            return;
        }

        await _settingManager.SetAsync(key, value, cancellationToken);
    }
}
