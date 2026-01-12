using System.Collections.Concurrent;

namespace Aonik.Application.Settings;

public static class SettingDefinitions
{
    private static readonly IReadOnlyDictionary<string, SettingDefinition> Definitions =
        new ConcurrentDictionary<string, SettingDefinition>(new Dictionary<string, SettingDefinition>
        {
            [AuthSettingNames.Provider] = new SettingDefinition(AuthSettingNames.Provider, "AzureAd", IsVisibleToClients: true),

            [AuthSettingNames.Auth0Domain] = new SettingDefinition(AuthSettingNames.Auth0Domain, IsVisibleToClients: true),
            [AuthSettingNames.Auth0Audience] = new SettingDefinition(AuthSettingNames.Auth0Audience, IsVisibleToClients: true),
            [AuthSettingNames.Auth0ClientId] = new SettingDefinition(AuthSettingNames.Auth0ClientId, IsVisibleToClients: true),
            [AuthSettingNames.Auth0ClientSecret] = new SettingDefinition(AuthSettingNames.Auth0ClientSecret, IsEncrypted: true),
            [AuthSettingNames.Auth0Connection] = new SettingDefinition(AuthSettingNames.Auth0Connection, IsVisibleToClients: true),
            [AuthSettingNames.Auth0ManagementAudience] = new SettingDefinition(AuthSettingNames.Auth0ManagementAudience),

            [AuthSettingNames.AzureAdAuthority] = new SettingDefinition(AuthSettingNames.AzureAdAuthority, IsVisibleToClients: true),
            [AuthSettingNames.AzureAdAudience] = new SettingDefinition(AuthSettingNames.AzureAdAudience, IsVisibleToClients: true),
            [AuthSettingNames.AzureAdClientId] = new SettingDefinition(AuthSettingNames.AzureAdClientId, IsVisibleToClients: true),
            [AuthSettingNames.AzureAdClientSecret] = new SettingDefinition(AuthSettingNames.AzureAdClientSecret, IsEncrypted: true),
            [AuthSettingNames.AzureAdTenantId] = new SettingDefinition(AuthSettingNames.AzureAdTenantId, IsVisibleToClients: true),
            [AuthSettingNames.AzureAdUpnDomain] = new SettingDefinition(AuthSettingNames.AzureAdUpnDomain)
        });

    public static SettingDefinition? Get(string key)
    {
        return Definitions.TryGetValue(key, out var definition) ? definition : null;
    }

    public static IReadOnlyCollection<SettingDefinition> All => Definitions.Values.ToList();
}
