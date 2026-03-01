using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Services.Settings;
using Aonik.Platform.Entities.Identity;

namespace Aonik.Infrastructure.Authentication.Account;

public class Auth0AccountService : IIdpAccountService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingProvider _settingProvider;

    public Auth0AccountService(HttpClient httpClient, ISettingProvider settingProvider)
    {
        _httpClient = httpClient;
        _settingProvider = settingProvider;
    }

    public async Task ValidatePasswordAsync(User user, string password, CancellationToken cancellationToken = default)
    {
        var domain = await _settingProvider.GetRequiredAsync(AuthSettingNames.Auth0Domain, cancellationToken);
        var clientId = await _settingProvider.GetRequiredAsync(AuthSettingNames.Auth0ClientId, cancellationToken);
        var audience = await _settingProvider.GetAsync(AuthSettingNames.Auth0Audience, cancellationToken);

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            throw new InvalidOperationException("User email is required to validate password.");
        }

        var payload = new Dictionary<string, string?>
        {
            ["grant_type"] = "password",
            ["client_id"] = clientId,
            ["username"] = user.Email,
            ["password"] = password,
            ["scope"] = "openid",
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
            throw new InvalidOperationException("Current password is invalid.");
        }
    }

    public async Task UpdateEmailAsync(User user, string newEmail, CancellationToken cancellationToken = default)
    {
        var baseUrl = await GetManagementBaseUrlAsync(cancellationToken);
        var token = await GetManagementTokenAsync(baseUrl, cancellationToken);
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new { email = newEmail, verify_email = true };
        var userId = Uri.EscapeDataString(user.ExternalSubject);

        using var response = await _httpClient.PatchAsJsonAsync(
            $"{baseUrl}/api/v2/users/{userId}",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Auth0 email update failed: {response.StatusCode} {error}");
        }
    }

    public async Task UpdatePasswordAsync(User user, string newPassword, CancellationToken cancellationToken = default)
    {
        var baseUrl = await GetManagementBaseUrlAsync(cancellationToken);
        var token = await GetManagementTokenAsync(baseUrl, cancellationToken);
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new { password = newPassword };
        var userId = Uri.EscapeDataString(user.ExternalSubject);

        using var response = await _httpClient.PatchAsJsonAsync(
            $"{baseUrl}/api/v2/users/{userId}",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Auth0 password update failed: {response.StatusCode} {error}");
        }
    }

    private async Task<string> GetManagementTokenAsync(string baseUrl, CancellationToken cancellationToken)
    {
        var clientId = await _settingProvider.GetRequiredAsync(AuthSettingNames.Auth0ManagementClientId, cancellationToken);
        var clientSecret = await _settingProvider.GetRequiredAsync(AuthSettingNames.Auth0ManagementClientSecret, cancellationToken);
        var managementAudience = await _settingProvider.GetAsync(AuthSettingNames.Auth0ManagementAudience, cancellationToken);

        var audience = string.IsNullOrWhiteSpace(managementAudience)
            ? $"{baseUrl}/api/v2/"
            : managementAudience.Trim();

        var tokenRequest = new
        {
            client_id = clientId,
            client_secret = clientSecret,
            audience,
            grant_type = "client_credentials"
        };

        using var response = await _httpClient.PostAsJsonAsync(
            $"{baseUrl}/oauth/token",
            tokenRequest,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Auth0 token request failed: {response.StatusCode} {error}");
        }

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        if (!payload.TryGetProperty("access_token", out var tokenElement))
        {
            throw new InvalidOperationException("Auth0 token response missing access_token.");
        }

        var token = tokenElement.GetString();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Auth0 token response empty.");
        }

        return token;
    }

    private async Task<string> GetManagementBaseUrlAsync(CancellationToken cancellationToken)
    {
        var domain = await _settingProvider.GetRequiredAsync(AuthSettingNames.Auth0Domain, cancellationToken);
        return NormalizeDomain(domain);
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
