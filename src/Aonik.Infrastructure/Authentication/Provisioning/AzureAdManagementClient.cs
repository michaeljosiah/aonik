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
/// Spec 026 Part 2 — Microsoft Entra ID implementation of
/// <see cref="IIdentityProviderManagementClient"/>. Uses Microsoft Graph
/// (<c>DELETE /v1.0/users/{id}</c>) with the application's
/// <c>User.ReadWrite.All</c> permission.
/// </summary>
public sealed class AzureAdManagementClient : IIdentityProviderManagementClient
{
    private readonly HttpClient _httpClient;
    private readonly ISettingProvider _settingProvider;
    private readonly ILogger<AzureAdManagementClient> _logger;

    public AzureAdManagementClient(
        HttpClient httpClient,
        ISettingProvider settingProvider,
        ILogger<AzureAdManagementClient> logger)
    {
        _httpClient = httpClient;
        _settingProvider = settingProvider;
        _logger = logger;
    }

    public string Provider => "AzureAd";

    public async Task<IdpDeleteUserResult> DeleteUserAsync(
        string externalSubject,
        string? externalTenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalSubject))
        {
            return new IdpDeleteUserResult(false, "external subject is missing");
        }

        string tenantId;
        string clientId;
        string clientSecret;
        try
        {
            tenantId = !string.IsNullOrWhiteSpace(externalTenantId)
                ? externalTenantId
                : await _settingProvider.GetRequiredAsync(AuthSettingNames.AzureAdTenantId, cancellationToken);
            clientId = await _settingProvider.GetRequiredAsync(AuthSettingNames.AzureAdClientId, cancellationToken);
            clientSecret = await _settingProvider.GetRequiredAsync(AuthSettingNames.AzureAdClientSecret, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure AD management settings missing — cannot delete external user {Subject}", externalSubject);
            return new IdpDeleteUserResult(false, $"missing setting: {ex.Message}");
        }

        string token;
        try
        {
            token = await GetGraphTokenAsync(tenantId, clientId, clientSecret, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure AD token acquisition failed for delete-user");
            return new IdpDeleteUserResult(false, $"token acquisition failed: {ex.Message}");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(externalSubject)}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Azure AD user {Subject} deleted (HTTP {Status})", externalSubject, (int)response.StatusCode);
                return new IdpDeleteUserResult(true, null);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Azure AD delete-user returned HTTP {Status} for {Subject}: {Body}",
                (int)response.StatusCode,
                externalSubject,
                body);
            return new IdpDeleteUserResult(false, $"HTTP {(int)response.StatusCode}: {Truncate(body, 200)}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure AD delete-user request failed for {Subject}", externalSubject);
            return new IdpDeleteUserResult(false, $"request failed: {ex.Message}");
        }
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

    private static string Truncate(string value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max] + "…";
}
