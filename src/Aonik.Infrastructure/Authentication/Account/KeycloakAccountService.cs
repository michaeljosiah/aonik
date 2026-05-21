using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Infrastructure.Authentication.Provisioning;
using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Services.Settings;
using Aonik.Platform.Entities.Identity;
using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Infrastructure.Authentication.Account;

/// <summary>
/// Spec 029 — Keycloak implementation of <see cref="IIdpAccountService"/>.
/// <list type="bullet">
///   <item><see cref="ValidatePasswordAsync"/> uses the realm's direct-grant
///         token endpoint (Resource Owner Password Credentials). Requires the
///         <c>aonik-spa</c> client to have "Direct Access Grants Enabled" — this is
///         a documented operator opt-in (see spec 029 §8.6).</item>
///   <item><see cref="UpdateEmailAsync"/> and <see cref="UpdatePasswordAsync"/>
///         use the Admin REST API with a GET-then-PUT pattern so unrelated
///         user fields aren't clobbered by partial PUT semantics in older
///         Keycloak versions.</item>
/// </list>
/// </summary>
public class KeycloakAccountService : IIdpAccountService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingProvider _settingProvider;

    public KeycloakAccountService(HttpClient httpClient, ISettingProvider settingProvider)
    {
        _httpClient = httpClient;
        _settingProvider = settingProvider;
    }

    public async Task ValidatePasswordAsync(User user, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            throw new InvalidOperationException("User email is required to validate password.");
        }

        var authority = await _settingProvider.GetRequiredAsync(AuthSettingNames.KeycloakAuthority, cancellationToken);
        var clientId = await _settingProvider.GetRequiredAsync(AuthSettingNames.KeycloakClientId, cancellationToken);
        var clientSecret = await _settingProvider.GetAsync(AuthSettingNames.KeycloakClientSecret, cancellationToken);

        var authorityBase = KeycloakUrls.NormalizeAuthority(authority);

        var payload = new Dictionary<string, string?>
        {
            ["grant_type"] = "password",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["username"] = user.Email,
            ["password"] = password,
            ["scope"] = "openid"
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
            throw new InvalidOperationException("Current password is invalid.");
        }
    }

    public async Task UpdateEmailAsync(User user, string newEmail, CancellationToken cancellationToken = default)
    {
        var (rootBase, realm, token) = await GetAdminContextAsync(cancellationToken);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Put,
            $"{rootBase}/admin/realms/{Uri.EscapeDataString(realm)}/users/{Uri.EscapeDataString(user.ExternalSubject)}")
        {
            Content = JsonContent.Create(new
            {
                email = newEmail,
                emailVerified = false,
                username = newEmail  // Keycloak's default username == email for password connections
            })
        };
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Keycloak email update failed: {response.StatusCode} {error}");
        }
    }

    public async Task UpdatePasswordAsync(User user, string newPassword, CancellationToken cancellationToken = default)
    {
        var (rootBase, realm, token) = await GetAdminContextAsync(cancellationToken);

        // Keycloak's reset-password endpoint takes a CredentialRepresentation directly.
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Put,
            $"{rootBase}/admin/realms/{Uri.EscapeDataString(realm)}/users/{Uri.EscapeDataString(user.ExternalSubject)}/reset-password")
        {
            Content = JsonContent.Create(new
            {
                type = "password",
                value = newPassword,
                temporary = false
            })
        };
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Keycloak password update failed: {response.StatusCode} {error}");
        }
    }

    private async Task<(string rootBase, string realm, string token)> GetAdminContextAsync(CancellationToken cancellationToken)
    {
        var authority = await _settingProvider.GetRequiredAsync(AuthSettingNames.KeycloakAuthority, cancellationToken);
        var realm = await _settingProvider.GetRequiredAsync(AuthSettingNames.KeycloakRealm, cancellationToken);
        var adminClientId = await _settingProvider.GetRequiredAsync(AuthSettingNames.KeycloakAdminClientId, cancellationToken);
        var adminClientSecret = await _settingProvider.GetRequiredAsync(AuthSettingNames.KeycloakAdminClientSecret, cancellationToken);

        var authorityBase = KeycloakUrls.NormalizeAuthority(authority);
        var rootBase = KeycloakUrls.RealmRoot(authorityBase);

        var token = await KeycloakTokenHelper.GetClientCredentialsTokenAsync(
            _httpClient, authorityBase, adminClientId, adminClientSecret, cancellationToken);

        return (rootBase, realm, token);
    }
}
