namespace Aonik.Platform.Services.Settings;

public static class AuthSettingNames
{
    public const string Provider = "Auth.Provider";

    public const string Auth0Domain = "Auth.Auth0.Domain";
    public const string Auth0Audience = "Auth.Auth0.Audience";
    public const string Auth0ClientId = "Auth.Auth0.ClientId";
    public const string Auth0ManagementClientId = "Auth.Auth0.ManagementClientId";
    public const string Auth0ManagementClientSecret = "Auth.Auth0.ManagementClientSecret";
    public const string Auth0Connection = "Auth.Auth0.Connection";
    public const string Auth0ManagementAudience = "Auth.Auth0.ManagementAudience";

    public const string AzureAdAuthority = "Auth.AzureAd.Authority";
    public const string AzureAdAudience = "Auth.AzureAd.Audience";
    public const string AzureAdClientId = "Auth.AzureAd.ClientId";
    public const string AzureAdClientSecret = "Auth.AzureAd.ClientSecret";
    public const string AzureAdTenantId = "Auth.AzureAd.TenantId";
    public const string AzureAdUpnDomain = "Auth.AzureAd.UserPrincipalNameDomain";

    // Spec 029 — Keycloak as a first-class operator-choice auth provider.
    // Authority is the full realm URL (e.g. https://keycloak.example.com/realms/aonik);
    // derived endpoint URLs are resolved at runtime via the realm's OIDC discovery
    // document, never assembled by hand. AdminClientId/Secret authenticate against
    // the realm's Admin API (service-accounts client with realm-management roles
    // manage-users + view-users + view-realm). See spec 029 §9 for setup details.
    public const string KeycloakAuthority = "Auth.Keycloak.Authority";
    public const string KeycloakAudience = "Auth.Keycloak.Audience";
    public const string KeycloakClientId = "Auth.Keycloak.ClientId";
    public const string KeycloakClientSecret = "Auth.Keycloak.ClientSecret";
    public const string KeycloakRealm = "Auth.Keycloak.Realm";
    public const string KeycloakAdminClientId = "Auth.Keycloak.AdminClientId";
    public const string KeycloakAdminClientSecret = "Auth.Keycloak.AdminClientSecret";
}
