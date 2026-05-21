using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Services.Settings;
using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Infrastructure.Authentication.Provisioning;

/// <summary>
/// Spec 026 Part 2 — selects the IdP management client at request time
/// based on the platform's active auth provider setting. Returns
/// <c>null</c> when the provider key isn't one we know how to manage
/// (e.g. local development without an IdP), letting the delete pipeline
/// continue without an IdP-side cleanup.
/// </summary>
public sealed class IdentityProviderManagementClientFactory : IIdentityProviderManagementClientFactory
{
    private readonly ISettingProvider _settingProvider;
    private readonly Auth0ManagementClient _auth0Client;
    private readonly AzureAdManagementClient _azureAdClient;
    private readonly KeycloakManagementClient _keycloakClient;

    public IdentityProviderManagementClientFactory(
        ISettingProvider settingProvider,
        Auth0ManagementClient auth0Client,
        AzureAdManagementClient azureAdClient,
        KeycloakManagementClient keycloakClient)
    {
        _settingProvider = settingProvider;
        _auth0Client = auth0Client;
        _azureAdClient = azureAdClient;
        _keycloakClient = keycloakClient;
    }

    public async Task<IIdentityProviderManagementClient?> GetClientAsync(CancellationToken cancellationToken = default)
    {
        var provider = await _settingProvider.GetAsync(AuthSettingNames.Provider, cancellationToken);
        if (string.IsNullOrWhiteSpace(provider))
        {
            return null;
        }

        return provider switch
        {
            "Auth0" => _auth0Client,
            "AzureAd" => _azureAdClient,
            "Keycloak" => _keycloakClient,
            _ => null,
        };
    }
}
