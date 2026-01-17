using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Application.Abstractions.Authentication;
using Aonik.Application.Abstractions.Settings;
using Aonik.Application.Models.Authentication;
using Aonik.Application.Settings;

namespace Aonik.Infrastructure.Authentication.TokenExchange;

public class AzureAdAuthTokenService : IAuthTokenService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingProvider _settingProvider;

    public AzureAdAuthTokenService(HttpClient httpClient, ISettingProvider settingProvider)
    {
        _httpClient = httpClient;
        _settingProvider = settingProvider;
    }

    public async Task<TokenResponse> ExchangeAsync(TokenRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = await _settingProvider.GetRequiredAsync(AuthSettingNames.AzureAdTenantId, cancellationToken);
        var clientId = await _settingProvider.GetRequiredAsync(AuthSettingNames.AzureAdClientId, cancellationToken);
        var clientSecret = await _settingProvider.GetAsync(AuthSettingNames.AzureAdClientSecret, cancellationToken);

        var payload = new Dictionary<string, string?>
        {
            ["grant_type"] = request.GrantType,
            ["client_id"] = string.IsNullOrWhiteSpace(request.ClientId) ? clientId : request.ClientId,
            ["client_secret"] = string.IsNullOrWhiteSpace(clientSecret) ? null : clientSecret,
            ["scope"] = request.Scope,
            ["redirect_uri"] = request.RedirectUri,
            ["code_verifier"] = request.CodeVerifier,
            ["code"] = request.AuthorizationCode,
            ["username"] = request.Username,
            ["password"] = request.Password
        };

        var content = new FormUrlEncodedContent(payload
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!));

        using var response = await _httpClient.PostAsync(
            $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token",
            content,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Azure AD token exchange failed: {response.StatusCode} {error}");
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
            throw new InvalidOperationException("Azure AD token response missing required fields.");
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
}
