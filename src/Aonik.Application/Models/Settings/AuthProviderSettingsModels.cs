namespace Aonik.Application.Models.Settings;

public record AuthProviderSettingsSnapshot(
    string ActiveProvider,
    Auth0SettingsSnapshot Auth0,
    AzureAdSettingsSnapshot AzureAd);

public record Auth0SettingsSnapshot(
    string? Domain,
    string? Audience,
    string? ClientId,
    bool HasClientSecret,
    string? Connection,
    string? ManagementAudience);

public record AzureAdSettingsSnapshot(
    string? Authority,
    string? Audience,
    string? ClientId,
    bool HasClientSecret,
    string? TenantId,
    string? UserPrincipalNameDomain);

public record AuthProviderSettingsUpdate(
    string ActiveProvider,
    Auth0SettingsUpdate? Auth0,
    AzureAdSettingsUpdate? AzureAd);

public record Auth0SettingsUpdate(
    string? Domain,
    string? Audience,
    string? ClientId,
    string? ClientSecret,
    string? Connection,
    string? ManagementAudience);

public record AzureAdSettingsUpdate(
    string? Authority,
    string? Audience,
    string? ClientId,
    string? ClientSecret,
    string? TenantId,
    string? UserPrincipalNameDomain);
