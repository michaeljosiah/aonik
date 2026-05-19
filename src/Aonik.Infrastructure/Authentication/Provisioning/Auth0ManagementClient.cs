using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Services.Settings;
using Aonik.SharedKernel.Abstractions.Settings;
using Microsoft.Extensions.Logging;

namespace Aonik.Infrastructure.Authentication.Provisioning;

/// <summary>
/// Spec 026 Part 2 — Auth0 implementation of
/// <see cref="IIdentityProviderManagementClient"/>. Reuses the same
/// client-credentials grant + audience as <see cref="Auth0UserProvisioner"/>,
/// but calls <c>DELETE /api/v2/users/{id}</c>.
/// </summary>
public sealed class Auth0ManagementClient : IIdentityProviderManagementClient
{
    private readonly HttpClient _httpClient;
    private readonly ISettingProvider _settingProvider;
    private readonly ILogger<Auth0ManagementClient> _logger;

    public Auth0ManagementClient(
        HttpClient httpClient,
        ISettingProvider settingProvider,
        ILogger<Auth0ManagementClient> logger)
    {
        _httpClient = httpClient;
        _settingProvider = settingProvider;
        _logger = logger;
    }

    public string Provider => "Auth0";

    public async Task<IdpDeleteUserResult> DeleteUserAsync(
        string externalSubject,
        string? externalTenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalSubject))
        {
            return new IdpDeleteUserResult(false, "external subject is missing");
        }

        string domain;
        string managementClientId;
        string managementClientSecret;
        string? managementAudience;
        try
        {
            domain = await _settingProvider.GetRequiredAsync(AuthSettingNames.Auth0Domain, cancellationToken);
            managementClientId = await _settingProvider.GetRequiredAsync(AuthSettingNames.Auth0ManagementClientId, cancellationToken);
            managementClientSecret = await _settingProvider.GetRequiredAsync(AuthSettingNames.Auth0ManagementClientSecret, cancellationToken);
            managementAudience = await _settingProvider.GetAsync(AuthSettingNames.Auth0ManagementAudience, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auth0 management settings missing — cannot delete external user {Subject}", externalSubject);
            return new IdpDeleteUserResult(false, $"missing setting: {ex.Message}");
        }

        var baseUrl = NormalizeDomain(domain);
        var audience = string.IsNullOrWhiteSpace(managementAudience)
            ? $"{baseUrl}/api/v2/"
            : managementAudience.Trim();

        string token;
        try
        {
            token = await GetManagementTokenAsync(baseUrl, managementClientId, managementClientSecret, audience, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auth0 management token acquisition failed for delete-user");
            return new IdpDeleteUserResult(false, $"token acquisition failed: {ex.Message}");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"{baseUrl}/api/v2/users/{Uri.EscapeDataString(externalSubject)}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Auth0 user {Subject} deleted (HTTP {Status})", externalSubject, (int)response.StatusCode);
                return new IdpDeleteUserResult(true, null);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Auth0 delete-user returned HTTP {Status} for {Subject}: {Body}",
                (int)response.StatusCode,
                externalSubject,
                body);
            return new IdpDeleteUserResult(false, $"HTTP {(int)response.StatusCode}: {Truncate(body, 200)}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auth0 delete-user request failed for {Subject}", externalSubject);
            return new IdpDeleteUserResult(false, $"request failed: {ex.Message}");
        }
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

    private static string Truncate(string value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max] + "…";
}
