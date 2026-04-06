namespace Aonik.Cli.Models;

public sealed record PublicAuthProviderSettingsResponse(
    string ActiveProvider,
    PublicAuth0SettingsResponse Auth0,
    PublicAzureAdSettingsResponse AzureAd);

public sealed record PublicAuth0SettingsResponse(
    string? Domain,
    string? Audience,
    string? ClientId,
    string? Connection);

public sealed record PublicAzureAdSettingsResponse(
    string? Authority,
    string? Audience,
    string? ClientId,
    string? TenantId);

public sealed record TokenRequestDto(
    string GrantType,
    string ClientId,
    string? Username,
    string? Password,
    string? Scope,
    string? RedirectUri,
    string? CodeVerifier,
    string? AuthorizationCode,
    string? RefreshToken);

public sealed record TokenResponseDto(
    string AccessToken,
    string? RefreshToken,
    int ExpiresIn,
    string TokenType,
    string? IdToken);

public sealed record UserInfoResponseDto(
    Guid UserId,
    string Email,
    string? FirstName,
    string? LastName,
    IReadOnlyCollection<string> Roles,
    Guid TenantId,
    Guid PartyId,
    string? PhotoUrl,
    string? PhotoUrlSmall,
    string? PhotoUrlTiny);

public sealed record CliSession(
    string BaseUrl,
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset? ExpiresAt,
    string? ActiveProvider,
    Guid? TenantId,
    Guid? UserId,
    string? Email,
    string? LastSessionId,
    string? LastThreadId);

public sealed record LoginOptions(
    string BaseUrl,
    string? Username,
    string? Password,
    string? AccessToken,
    string? ClientId,
    string? Scope,
    Guid? TenantId,
    OutputMode OutputMode);
