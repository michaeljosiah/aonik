namespace Aonik.Platform.Contracts.Models.Authentication;

public record TokenRequest(
    string GrantType,
    string ClientId,
    string? Username,
    string? Password,
    string? Scope,
    string? RedirectUri,
    string? CodeVerifier,
    string? AuthorizationCode);

public record TokenResponse(
    string AccessToken,
    string? RefreshToken,
    int ExpiresIn,
    string TokenType,
    string? IdToken);
