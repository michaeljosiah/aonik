using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Contracts.Models.Authentication;
using Aonik.Application.Settings;

namespace Aonik.Infrastructure.Authentication.Provisioning;

public class AzureAdUserProvisioner : IIdpUserProvisioner
{
    private readonly HttpClient _httpClient;
    private readonly ISettingProvider _settingProvider;

    public AzureAdUserProvisioner(HttpClient httpClient, ISettingProvider settingProvider)
    {
        _httpClient = httpClient;
        _settingProvider = settingProvider;
    }

    public async Task<ExternalIdentityResult> CreateUserAsync(
        IdpUserRegistration registration,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _settingProvider.GetRequiredAsync(AuthSettingNames.AzureAdTenantId, cancellationToken);
        var clientId = await _settingProvider.GetRequiredAsync(AuthSettingNames.AzureAdClientId, cancellationToken);
        var clientSecret = await _settingProvider.GetRequiredAsync(AuthSettingNames.AzureAdClientSecret, cancellationToken);
        var upnDomain = await _settingProvider.GetRequiredAsync(AuthSettingNames.AzureAdUpnDomain, cancellationToken);

        var authority = await _settingProvider.GetAsync(AuthSettingNames.AzureAdAuthority, cancellationToken)
                        ?? $"https://login.microsoftonline.com/{tenantId}/v2.0";

        var token = await GetGraphTokenAsync(tenantId, clientId, clientSecret, cancellationToken);
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var displayName = BuildDisplayName(registration.FirstName, registration.LastName);
        var mailNickname = BuildMailNickname(registration.Email);
        var userPrincipalName = $"{mailNickname}@{upnDomain}";

        var request = new
        {
            accountEnabled = true,
            displayName,
            mailNickname,
            userPrincipalName,
            mail = registration.Email,
            otherMails = new[] { registration.Email },
            givenName = registration.FirstName,
            surname = registration.LastName,
            passwordProfile = new
            {
                forceChangePasswordNextSignIn = false,
                password = registration.Password
            }
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "https://graph.microsoft.com/v1.0/users",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Azure AD user creation failed: {response.StatusCode} {error}");
        }

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var userId = payload.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException("Azure AD did not return an id.");
        }

        return new ExternalIdentityResult(authority, userId, tenantId);
    }

    private async Task<string> GetGraphTokenAsync(
        string tenantId,
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken)
    {
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

    private static string BuildDisplayName(string firstName, string lastName)
    {
        return string.Join(' ', new[] { firstName?.Trim(), lastName?.Trim() }
            .Where(part => !string.IsNullOrWhiteSpace(part))!);
    }

    private static string BuildMailNickname(string email)
    {
        var local = email.Split('@')[0];
        var sanitized = new string(local.Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "user" + Guid.NewGuid().ToString("N")[..8] : sanitized;
    }
}
