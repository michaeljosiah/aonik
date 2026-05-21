using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Infrastructure.Authentication.Provisioning;
using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Contracts.Models.Authentication;
using Aonik.Platform.Services.Settings;
using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Infrastructure.Authentication.TokenExchange;

/// <summary>
/// Spec 029 — Keycloak implementation of <see cref="IAuthTokenService"/>. POSTs to
/// the realm's <c>/protocol/openid-connect/token</c> endpoint with a standard OIDC
/// grant. Supports password / authorization_code / refresh_token grants — the operator
/// chooses which to enable on the <c>aonik-spa</c> client. Direct grant (password) is
/// off by default in Keycloak and must be turned on explicitly per spec 029 §8.6.
/// </summary>
public class KeycloakAuthTokenService : IAuthTokenService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingProvider _settingProvider;

    public KeycloakAuthTokenService(HttpClient httpClient, ISettingProvider settingProvider)
    {
        _httpClient = httpClient;
        _settingProvider = settingProvider;
    }

    public async Task<TokenResponse> ExchangeAsync(TokenRequest request, CancellationToken cancellationToken = default)
    {
        var authority = await _settingProvider.GetRequiredAsync(AuthSettingNames.KeycloakAuthority, cancellationToken);
        var configuredClientId = await _settingProvider.GetRequiredAsync(AuthSettingNames.KeycloakClientId, cancellationToken);
        var clientSecret = await _settingProvider.GetAsync(AuthSettingNames.KeycloakClientSecret, cancellationToken);

        var authorityBase = KeycloakUrls.NormalizeAuthority(authority);

        var effectiveClientId = string.IsNullOrWhiteSpace(request.ClientId) ? configuredClientId : request.ClientId;

        var payload = new Dictionary<string, string?>
        {
            ["grant_type"] = request.GrantType,
            ["client_id"] = effectiveClientId,
            ["client_secret"] = string.IsNullOrWhiteSpace(clientSecret) ? null : clientSecret,
            ["username"] = request.Username,
            ["password"] = request.Password,
            ["scope"] = request.Scope,
            ["redirect_uri"] = request.RedirectUri,
            ["code_verifier"] = request.CodeVerifier,
            ["code"] = request.AuthorizationCode,
            ["refresh_token"] = request.RefreshToken
        };

        var content = new FormUrlEncodedContent(payload
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!));

        using var response = await _httpClient.PostAsync(
            $"{authorityBase}/protocol/openid-connect/token",
            content,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Keycloak token exchange failed: {response.StatusCode} {error}");
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
            throw new InvalidOperationException("Keycloak token response missing required fields.");
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
