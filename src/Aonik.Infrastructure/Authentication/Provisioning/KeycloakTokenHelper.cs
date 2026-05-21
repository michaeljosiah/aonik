using System.Net.Http.Json;
using System.Text.Json;

namespace Aonik.Infrastructure.Authentication.Provisioning;

/// <summary>
/// Spec 029 — shared client-credentials token acquisition for Keycloak. Identical
/// flow used by every Keycloak* service that calls the Admin REST API:
/// <c>POST {realm}/protocol/openid-connect/token</c> with <c>grant_type=client_credentials</c>.
/// Lives once here instead of being duplicated across five files.
/// </summary>
internal static class KeycloakTokenHelper
{
    public static async Task<string> GetClientCredentialsTokenAsync(
        HttpClient httpClient,
        string authorityBase,
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{authorityBase}/protocol/openid-connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["grant_type"] = "client_credentials"
            })
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Keycloak token request failed: {response.StatusCode} {error}");
        }

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        if (!payload.TryGetProperty("access_token", out var tokenElement))
        {
            throw new InvalidOperationException("Keycloak token response missing access_token.");
        }

        var token = tokenElement.GetString();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Keycloak token response empty.");
        }

        return token;
    }
}
