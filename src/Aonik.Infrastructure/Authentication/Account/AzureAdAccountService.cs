using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Application.Abstractions.Authentication;
using Aonik.Application.Abstractions.Settings;
using Aonik.Application.Settings;
using Aonik.Platform.Entities.Identity;

namespace Aonik.Infrastructure.Authentication.Account;

public class AzureAdAccountService : IIdpAccountService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingProvider _settingProvider;

    public AzureAdAccountService(HttpClient httpClient, ISettingProvider settingProvider)
    {
        _httpClient = httpClient;
        _settingProvider = settingProvider;
    }

    public async Task ValidatePasswordAsync(User user, string password, CancellationToken cancellationToken = default)
    {
        var tenantId = await _settingProvider.GetRequiredAsync(AuthSettingNames.AzureAdTenantId, cancellationToken);
        var clientId = await _settingProvider.GetRequiredAsync(AuthSettingNames.AzureAdClientId, cancellationToken);
        var clientSecret = await _settingProvider.GetAsync(AuthSettingNames.AzureAdClientSecret, cancellationToken);

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            throw new InvalidOperationException("User email is required to validate password.");
        }

        var payload = new Dictionary<string, string?>
        {
            ["grant_type"] = "password",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = "https://graph.microsoft.com/.default",
            ["username"] = user.Email,
            ["password"] = password
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
            throw new InvalidOperationException("Current password is invalid.");
        }
    }

    public async Task UpdateEmailAsync(User user, string newEmail, CancellationToken cancellationToken = default)
    {
        var token = await GetGraphTokenAsync(cancellationToken);
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new { mail = newEmail, userPrincipalName = newEmail, otherMails = new[] { newEmail } };

        using var response = await _httpClient.PatchAsJsonAsync(
            $"https://graph.microsoft.com/v1.0/users/{user.ExternalSubject}",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Azure AD email update failed: {response.StatusCode} {error}");
        }
    }

    public async Task UpdatePasswordAsync(User user, string newPassword, CancellationToken cancellationToken = default)
    {
        var token = await GetGraphTokenAsync(cancellationToken);
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new
        {
            passwordProfile = new
            {
                forceChangePasswordNextSignIn = false,
                password = newPassword
            }
        };

        using var response = await _httpClient.PatchAsJsonAsync(
            $"https://graph.microsoft.com/v1.0/users/{user.ExternalSubject}",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Azure AD password update failed: {response.StatusCode} {error}");
        }
    }

    private async Task<string> GetGraphTokenAsync(CancellationToken cancellationToken)
    {
        var tenantId = await _settingProvider.GetRequiredAsync(AuthSettingNames.AzureAdTenantId, cancellationToken);
        var clientId = await _settingProvider.GetRequiredAsync(AuthSettingNames.AzureAdClientId, cancellationToken);
        var clientSecret = await _settingProvider.GetRequiredAsync(AuthSettingNames.AzureAdClientSecret, cancellationToken);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["scope"] = "https://graph.microsoft.com/.default",
                ["grant_type"] = "client_credentials"
            })
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Azure AD token request failed: {response.StatusCode} {error}");
        }

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        if (!payload.TryGetProperty("access_token", out var tokenElement))
        {
            throw new InvalidOperationException("Azure AD token response missing access_token.");
        }

        var token = tokenElement.GetString();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Azure AD token response empty.");
        }

        return token;
    }
}
