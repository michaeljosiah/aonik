namespace Aonik.Platform.Contracts.Api.Settings;

public record AuthProviderSettingsResponse(
    string ActiveProvider,
    Auth0SettingsResponse Auth0,
    AzureAdSettingsResponse AzureAd,
    KeycloakSettingsResponse Keycloak);

public record Auth0SettingsResponse(
    string? Domain,
    string? Audience,
    string? ClientId,
    string? ManagementClientId,
    bool HasManagementClientSecret,
    string? Connection,
    string? ManagementAudience);

public record AzureAdSettingsResponse(
    string? Authority,
    string? Audience,
    string? ClientId,
    bool HasClientSecret,
    string? TenantId,
    string? UserPrincipalNameDomain);

public record KeycloakSettingsResponse(
    string? Authority,
    string? Audience,
    string? ClientId,
    bool HasClientSecret,
    string? Realm,
    string? AdminClientId,
    bool HasAdminClientSecret);

public record AuthProviderSettingsUpdateRequest(
    string ActiveProvider,
    Auth0SettingsUpdateRequest? Auth0,
    AzureAdSettingsUpdateRequest? AzureAd,
    KeycloakSettingsUpdateRequest? Keycloak);

public record Auth0SettingsUpdateRequest(
    string? Domain,
    string? Audience,
    string? ClientId,
    string? ManagementClientId,
    string? ManagementClientSecret,
    string? Connection,
    string? ManagementAudience);

public record AzureAdSettingsUpdateRequest(
    string? Authority,
    string? Audience,
    string? ClientId,
    string? ClientSecret,
    string? TenantId,
    string? UserPrincipalNameDomain);

public record KeycloakSettingsUpdateRequest(
    string? Authority,
    string? Audience,
    string? ClientId,
    string? ClientSecret,
    string? Realm,
    string? AdminClientId,
    string? AdminClientSecret);
