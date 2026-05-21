using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Infrastructure.Authentication.Provisioning;
using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Services.Settings;
using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Infrastructure.Authentication.PasswordReset;

/// <summary>
/// Spec 029 — Keycloak implementation of <see cref="IIdpPasswordResetService"/>.
/// Looks up the user by email via the Admin REST API, then dispatches an
/// <c>UPDATE_PASSWORD</c> required-action email through Keycloak's configured SMTP.
/// Aonik never sees the actual reset URL — Keycloak generates and mails the action
/// token, and the user clicks through Keycloak's hosted reset UI.
/// </summary>
public class KeycloakPasswordResetService : IIdpPasswordResetService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingProvider _settingProvider;

    public KeycloakPasswordResetService(HttpClient httpClient, ISettingProvider settingProvider)
    {
        _httpClient = httpClient;
        _settingProvider = settingProvider;
    }

    public async Task TriggerResetAsync(string email, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var authority = await _settingProvider.GetRequiredAsync(AuthSettingNames.KeycloakAuthority, cancellationToken);
        var realm = await _settingProvider.GetRequiredAsync(AuthSettingNames.KeycloakRealm, cancellationToken);
        var adminClientId = await _settingProvider.GetRequiredAsync(AuthSettingNames.KeycloakAdminClientId, cancellationToken);
        var adminClientSecret = await _settingProvider.GetRequiredAsync(AuthSettingNames.KeycloakAdminClientSecret, cancellationToken);

        var authorityBase = KeycloakUrls.NormalizeAuthority(authority);
        var rootBase = KeycloakUrls.RealmRoot(authorityBase);

        var token = await KeycloakTokenHelper.GetClientCredentialsTokenAsync(
            _httpClient, authorityBase, adminClientId, adminClientSecret, cancellationToken);

        var userId = await FindUserIdByEmailAsync(rootBase, realm, email, token, cancellationToken);
        if (string.IsNullOrWhiteSpace(userId))
        {
            // Mirror the Auth0 contract: don't leak "no such user" to callers — Keycloak's
            // own /dbconnections/change_password equivalent silently succeeds on missing
            // emails, and we preserve that semantic so password reset can't be used as a
            // user-enumeration oracle.
            return;
        }

        using var actionRequest = new HttpRequestMessage(
            HttpMethod.Put,
            $"{rootBase}/admin/realms/{Uri.EscapeDataString(realm)}/users/{Uri.EscapeDataString(userId)}/execute-actions-email")
        {
            Content = JsonContent.Create(new[] { "UPDATE_PASSWORD" })
        };
        actionRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(actionRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Keycloak password reset failed: {response.StatusCode} {error}");
        }
    }

    private async Task<string?> FindUserIdByEmailAsync(
        string rootBase,
        string realm,
        string email,
        string token,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{rootBase}/admin/realms/{Uri.EscapeDataString(realm)}/users?email={Uri.EscapeDataString(email)}&exact=true");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Keycloak user lookup failed: {response.StatusCode} {error}");
        }

        var users = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        if (users.ValueKind != JsonValueKind.Array || users.GetArrayLength() == 0)
        {
            return null;
        }

        var first = users.EnumerateArray().First();
        return first.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
    }
}
