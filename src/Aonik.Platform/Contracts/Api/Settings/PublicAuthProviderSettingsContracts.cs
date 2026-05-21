namespace Aonik.Platform.Contracts.Api.Settings;

public record PublicAuthProviderSettingsResponse(
    string ActiveProvider,
    PublicAuth0SettingsResponse Auth0,
    PublicAzureAdSettingsResponse AzureAd,
    PublicKeycloakSettingsResponse Keycloak);

public record PublicAuth0SettingsResponse(
    string? Domain,
    string? Audience,
    string? ClientId,
    string? Connection);

public record PublicAzureAdSettingsResponse(
    string? Authority,
    string? Audience,
    string? ClientId,
    string? TenantId);

public record PublicKeycloakSettingsResponse(
    string? Authority,
    string? Audience,
    string? ClientId,
    string? Realm);
