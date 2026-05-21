namespace Aonik.Platform.Contracts.Models.Settings;

public record AuthProviderSettingsSnapshot(
    string ActiveProvider,
    Auth0SettingsSnapshot Auth0,
    AzureAdSettingsSnapshot AzureAd,
    KeycloakSettingsSnapshot Keycloak);

public record Auth0SettingsSnapshot(
    string? Domain,
    string? Audience,
    string? ClientId,
    string? ManagementClientId,
    bool HasManagementClientSecret,
    string? Connection,
    string? ManagementAudience);

public record AzureAdSettingsSnapshot(
    string? Authority,
    string? Audience,
    string? ClientId,
    bool HasClientSecret,
    string? TenantId,
    string? UserPrincipalNameDomain);

// Spec 029 — Keycloak settings snapshot returned to the admin UI. Secret
// fields use the Has{Name} boolean pattern: the secret itself never
// round-trips through the snapshot, only an indicator that one is set.
public record KeycloakSettingsSnapshot(
    string? Authority,
    string? Audience,
    string? ClientId,
    bool HasClientSecret,
    string? Realm,
    string? AdminClientId,
    bool HasAdminClientSecret);

public record AuthProviderSettingsUpdate(
    string ActiveProvider,
    Auth0SettingsUpdate? Auth0,
    AzureAdSettingsUpdate? AzureAd,
    KeycloakSettingsUpdate? Keycloak);

public record Auth0SettingsUpdate(
    string? Domain,
    string? Audience,
    string? ClientId,
    string? ManagementClientId,
    string? ManagementClientSecret,
    string? Connection,
    string? ManagementAudience);

public record AzureAdSettingsUpdate(
    string? Authority,
    string? Audience,
    string? ClientId,
    string? ClientSecret,
    string? TenantId,
    string? UserPrincipalNameDomain);

public record KeycloakSettingsUpdate(
    string? Authority,
    string? Audience,
    string? ClientId,
    string? ClientSecret,
    string? Realm,
    string? AdminClientId,
    string? AdminClientSecret);
