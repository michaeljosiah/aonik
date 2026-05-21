using System.Net;
using System.Net.Http.Json;

using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Registration;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Contracts.Models.Authentication;
using Aonik.Platform.Services.Settings;
using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Infrastructure.Authentication.Provisioning;

/// <summary>
/// Spec 029 — Keycloak implementation of <see cref="IIdpUserProvisioner"/>.
/// Posts a Keycloak user representation to <c>POST /admin/realms/{realm}/users</c>
/// with an initial password credential. Keycloak responds 201 with the new user's
/// UUID in the <c>Location</c> header (the response body is empty), so the
/// canonical ID extraction is "parse the last segment of the Location URL."
/// </summary>
public class KeycloakUserProvisioner : IIdpUserProvisioner
{
    private readonly HttpClient _httpClient;
    private readonly ISettingProvider _settingProvider;

    public KeycloakUserProvisioner(HttpClient httpClient, ISettingProvider settingProvider)
    {
        _httpClient = httpClient;
        _settingProvider = settingProvider;
    }

    public async Task<ExternalIdentityResult> CreateUserAsync(
        IdpUserRegistration registration,
        CancellationToken cancellationToken = default)
    {
        var authority = await _settingProvider.GetRequiredAsync(AuthSettingNames.KeycloakAuthority, cancellationToken);
        var realm = await _settingProvider.GetRequiredAsync(AuthSettingNames.KeycloakRealm, cancellationToken);
        var adminClientId = await _settingProvider.GetRequiredAsync(AuthSettingNames.KeycloakAdminClientId, cancellationToken);
        var adminClientSecret = await _settingProvider.GetRequiredAsync(AuthSettingNames.KeycloakAdminClientSecret, cancellationToken);

        var authorityBase = KeycloakUrls.NormalizeAuthority(authority);
        var rootBase = KeycloakUrls.RealmRoot(authorityBase);

        var token = await KeycloakTokenHelper.GetClientCredentialsTokenAsync(
            _httpClient, authorityBase, adminClientId, adminClientSecret, cancellationToken);

        var userRepresentation = new
        {
            username = registration.Email,
            email = registration.Email,
            firstName = registration.FirstName,
            lastName = registration.LastName,
            enabled = true,
            emailVerified = false,
            credentials = new[]
            {
                new
                {
                    type = "password",
                    value = registration.Password,
                    temporary = false
                }
            }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{rootBase}/admin/realms/{Uri.EscapeDataString(realm)}/users")
        {
            Content = JsonContent.Create(userRepresentation)
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                throw new RegistrationConflictException(
                    "An account with this email address already exists.");
            }

            throw new InvalidOperationException($"Keycloak user creation failed: {response.StatusCode} {error}");
        }

        var userId = ExtractUserIdFromLocation(response.Headers.Location);
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException("Keycloak did not return a Location header containing the new user id.");
        }

        // Issuer matches the JWT 'iss' claim that Keycloak emits: the full
        // realm URL with no trailing slash. Keep parity with how the issuer
        // appears in tokens so the Aonik-side user record's ExternalIssuer
        // round-trips through HandleTokenValidatedAsync without divergence.
        return new ExternalIdentityResult(authorityBase, userId, realm);
    }

    private static string? ExtractUserIdFromLocation(Uri? location)
    {
        if (location is null)
        {
            return null;
        }

        var path = location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString;
        var segments = path.TrimEnd('/').Split('/');
        return segments.Length == 0 ? null : segments[^1];
    }
}
