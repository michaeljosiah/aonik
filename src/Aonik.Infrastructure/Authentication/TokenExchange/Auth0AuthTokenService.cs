using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Contracts.Models.Authentication;
using Aonik.Platform.Services.Settings;

namespace Aonik.Infrastructure.Authentication.TokenExchange;

public class Auth0AuthTokenService : IAuthTokenService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingProvider _settingProvider;

    public Auth0AuthTokenService(HttpClient httpClient, ISettingProvider settingProvider)
    {
        _httpClient = httpClient;
        _settingProvider = settingProvider;
    }

    public async Task<TokenResponse> ExchangeAsync(TokenRequest request, CancellationToken cancellationToken = default)
    {
        var domain = await _settingProvider.GetRequiredAsync(AuthSettingNames.Auth0Domain, cancellationToken);
        var clientId = await _settingProvider.GetRequiredAsync(AuthSettingNames.Auth0ClientId, cancellationToken);
        var clientSecret = await _settingProvider.GetAsync(AuthSettingNames.Auth0ClientSecret, cancellationToken);
        var audience = await _settingProvider.GetAsync(AuthSettingNames.Auth0Audience, cancellationToken);

        var effectiveClientId = string.IsNullOrWhiteSpace(request.ClientId)
            ? clientId
            : request.ClientId;

        var shouldSendClientSecret = !string.IsNullOrWhiteSpace(clientSecret)
            && string.Equals(effectiveClientId, clientId, StringComparison.Ordinal);

        var payload = new Dictionary<string, string?>
        {
            ["grant_type"] = request.GrantType,
            ["client_id"] = effectiveClientId,
            ["client_secret"] = shouldSendClientSecret ? clientSecret : null,
            ["username"] = request.Username,
            ["password"] = request.Password,
            ["scope"] = request.Scope,
            ["redirect_uri"] = request.RedirectUri,
            ["code_verifier"] = request.CodeVerifier,
            ["code"] = request.AuthorizationCode,
            ["audience"] = audience
        };

        var content = new FormUrlEncodedContent(payload
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!));

        var baseUrl = NormalizeDomain(domain);
        using var response = await _httpClient.PostAsync(
            $"{baseUrl}/oauth/token",
            content,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Auth0 token exchange failed: {response.StatusCode} {error}");
        }

        var payloadJson = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        return MapResponse(payloadJson);
    }

    private static TokenResponse MapResponse(JsonElement payload)
    {
        var accessToken = payload.GetProperty("access_token").GetString();
        var expiresIn = payload.TryGetProperty("expires_in", out var expiresElement)
            ? expiresElement.GetInt32()
            : 0;
        var tokenType = payload.TryGetProperty("token_type", out var tokenTypeElement)
            ? tokenTypeElement.GetString()
            : "Bearer";

        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(tokenType))
        {
            throw new InvalidOperationException("Auth0 token response missing required fields.");
        }

        var refreshToken = payload.TryGetProperty("refresh_token", out var refreshTokenElement)
            ? refreshTokenElement.GetString()
            : null;
        var idToken = payload.TryGetProperty("id_token", out var idTokenElement)
            ? idTokenElement.GetString()
            : null;

        return new TokenResponse(
            accessToken,
            refreshToken,
            expiresIn,
            tokenType,
            idToken);
    }

    private static string NormalizeDomain(string domain)
    {
        var trimmed = domain.Trim().TrimEnd('/');
        if (trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return $"https://{trimmed}";
    }
}
