using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Registration;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Contracts.Models.Authentication;
using Aonik.Platform.Services.Settings;

namespace Aonik.Infrastructure.Authentication.Provisioning;

public class Auth0UserProvisioner : IIdpUserProvisioner
{
    private readonly HttpClient _httpClient;
    private readonly ISettingProvider _settingProvider;

    public Auth0UserProvisioner(HttpClient httpClient, ISettingProvider settingProvider)
    {
        _httpClient = httpClient;
        _settingProvider = settingProvider;
    }

    public async Task<ExternalIdentityResult> CreateUserAsync(
        IdpUserRegistration registration,
        CancellationToken cancellationToken = default)
    {
        var domain = await _settingProvider.GetRequiredAsync(AuthSettingNames.Auth0Domain, cancellationToken);
        var managementClientId = await _settingProvider.GetRequiredAsync(AuthSettingNames.Auth0ManagementClientId, cancellationToken);
        var managementClientSecret = await _settingProvider.GetRequiredAsync(AuthSettingNames.Auth0ManagementClientSecret, cancellationToken);
        var connection = await _settingProvider.GetRequiredAsync(AuthSettingNames.Auth0Connection, cancellationToken);
        var managementAudience = await _settingProvider.GetAsync(AuthSettingNames.Auth0ManagementAudience, cancellationToken);

        var baseUrl = NormalizeDomain(domain);
        var audience = string.IsNullOrWhiteSpace(managementAudience)
            ? $"{baseUrl}/api/v2/"
            : managementAudience.Trim();

        var accessToken = await GetManagementTokenAsync(baseUrl, managementClientId, managementClientSecret, audience, cancellationToken);
        var request = new
        {
            email = registration.Email,
            password = registration.Password,
            connection,
            given_name = registration.FirstName,
            family_name = registration.LastName,
            name = BuildDisplayName(registration.FirstName, registration.LastName)
        };

        using var response = await _httpClient.PostAsJsonAsync(
            $"{baseUrl}/api/v2/users",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                throw new RegistrationConflictException(
                    "An account with this email address already exists.");
            }

            throw new InvalidOperationException($"Auth0 user creation failed: {response.StatusCode} {error}");
        }

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var userId = payload.GetProperty("user_id").GetString();
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException("Auth0 did not return a user_id.");
        }

        var issuer = $"{baseUrl}/";
        return new ExternalIdentityResult(issuer, userId, null);
    }

    private async Task<string> GetManagementTokenAsync(
        string baseUrl,
        string clientId,
        string clientSecret,
        string audience,
        CancellationToken cancellationToken)
    {
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

        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return token;
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

    private static string BuildDisplayName(string firstName, string lastName)
    {
        return string.Join(' ', new[] { firstName?.Trim(), lastName?.Trim() }
            .Where(part => !string.IsNullOrWhiteSpace(part))!);
    }
}
