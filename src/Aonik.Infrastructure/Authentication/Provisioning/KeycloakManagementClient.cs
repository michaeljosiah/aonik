using System.Net;

using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Services.Settings;
using Aonik.SharedKernel.Abstractions.Settings;
using Microsoft.Extensions.Logging;

namespace Aonik.Infrastructure.Authentication.Provisioning;

/// <summary>
/// Spec 029 — Keycloak implementation of <see cref="IIdentityProviderManagementClient"/>.
/// Calls <c>DELETE /admin/realms/{realm}/users/{userId}</c> on the Keycloak Admin REST API,
/// authenticated with a client-credentials token from the admin service-account client.
/// Mirrors the failure-mode semantics of <see cref="Auth0ManagementClient"/>: 204 / 404 →
/// success, all other statuses → structured failure result.
/// </summary>
public sealed class KeycloakManagementClient : IIdentityProviderManagementClient
{
    private readonly HttpClient _httpClient;
    private readonly ISettingProvider _settingProvider;
    private readonly ILogger<KeycloakManagementClient> _logger;

    public KeycloakManagementClient(
        HttpClient httpClient,
        ISettingProvider settingProvider,
        ILogger<KeycloakManagementClient> logger)
    {
        _httpClient = httpClient;
        _settingProvider = settingProvider;
        _logger = logger;
    }

    public string Provider => "Keycloak";

    public async Task<IdpDeleteUserResult> DeleteUserAsync(
        string externalSubject,
        string? externalTenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalSubject))
        {
            return new IdpDeleteUserResult(false, "external subject is missing");
        }

        string authority;
        string realm;
        string adminClientId;
        string adminClientSecret;
        try
        {
            authority = await _settingProvider.GetRequiredAsync(AuthSettingNames.KeycloakAuthority, cancellationToken);
            realm = await _settingProvider.GetRequiredAsync(AuthSettingNames.KeycloakRealm, cancellationToken);
            adminClientId = await _settingProvider.GetRequiredAsync(AuthSettingNames.KeycloakAdminClientId, cancellationToken);
            adminClientSecret = await _settingProvider.GetRequiredAsync(AuthSettingNames.KeycloakAdminClientSecret, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Keycloak management settings missing — cannot delete external user {Subject}", externalSubject);
            return new IdpDeleteUserResult(false, $"missing setting: {ex.Message}");
        }

        var authorityBase = KeycloakUrls.NormalizeAuthority(authority);
        var rootBase = KeycloakUrls.RealmRoot(authorityBase);

        string token;
        try
        {
            token = await KeycloakTokenHelper.GetClientCredentialsTokenAsync(
                _httpClient, authorityBase, adminClientId, adminClientSecret, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Keycloak admin token acquisition failed for delete-user");
            return new IdpDeleteUserResult(false, $"token acquisition failed: {ex.Message}");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"{rootBase}/admin/realms/{Uri.EscapeDataString(realm)}/users/{Uri.EscapeDataString(externalSubject)}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation(
                    "Keycloak user {Subject} deleted from realm {Realm} (HTTP {Status})",
                    externalSubject, realm, (int)response.StatusCode);
                return new IdpDeleteUserResult(true, null);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Keycloak delete-user returned HTTP {Status} for {Subject}: {Body}",
                (int)response.StatusCode,
                externalSubject,
                body);
            return new IdpDeleteUserResult(false, $"HTTP {(int)response.StatusCode}: {Truncate(body, 200)}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Keycloak delete-user request failed for {Subject}", externalSubject);
            return new IdpDeleteUserResult(false, $"request failed: {ex.Message}");
        }
    }

    private static string Truncate(string value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max] + "…";
}
