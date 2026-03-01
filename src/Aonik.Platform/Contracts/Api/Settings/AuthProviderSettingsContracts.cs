namespace Aonik.Platform.Contracts.Api.Settings;

public record AuthProviderSettingsResponse(
    string ActiveProvider,
    Auth0SettingsResponse Auth0,
    AzureAdSettingsResponse AzureAd);

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

public record AuthProviderSettingsUpdateRequest(
    string ActiveProvider,
    Auth0SettingsUpdateRequest? Auth0,
    AzureAdSettingsUpdateRequest? AzureAd);

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
