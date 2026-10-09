using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Options;

using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Services.Settings;
using Aonik.Platform.Entities.Identity;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.Infrastructure.Authentication.Configuration;
using OpenTelemetry;

namespace Aonik.Infrastructure.Authentication.Account;

public class Auth0AccountService : IIdpAccountService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingProvider _settingProvider;
    private readonly IOptions<AuthOptions>? _authOptions;

    public Auth0AccountService(HttpClient httpClient, ISettingProvider settingProvider, IOptions<AuthOptions>? authOptions = null)
    {
        _httpClient = httpClient;
        _settingProvider = settingProvider;
        _authOptions = authOptions;
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

        var userId = Uri.EscapeDataString(user.ExternalSubject);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Patch, $"{baseUrl}/api/v2/users/{userId}");
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        httpRequest.Content = JsonContent.Create(new { email = newEmail, verify_email = true });

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

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

        var userId = Uri.EscapeDataString(user.ExternalSubject);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Patch, $"{baseUrl}/api/v2/users/{userId}");
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        httpRequest.Content = JsonContent.Create(new { password = newPassword });

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Auth0 password update failed: {response.StatusCode} {error}");
        }
    }

    public async Task ConfirmVerifiedEmailAsync(User user, string expectedCurrentEmail, string newEmail,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // The subject in the URL and provider bodies are identity data, not HTTP telemetry.
        using var suppression = SuppressInstrumentationScope.Begin();
        var baseUrl = await GetManagementBaseUrlAsync(cancellationToken);
        var authority = _authOptions?.Value.Auth0.Authority;
        if (string.IsNullOrWhiteSpace(authority)) authority = baseUrl;
        var connection = await _settingProvider.GetRequiredAsync(AuthSettingNames.Auth0Connection, cancellationToken);
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https"
            || endpoint.AbsolutePath != "/" || !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment)
            || !string.IsNullOrEmpty(endpoint.UserInfo)
            || !string.Equals(user.ExternalIssuer.TrimEnd('/'), authority.TrimEnd('/'), StringComparison.Ordinal)
            || !user.ExternalSubject.StartsWith("auth0|", StringComparison.Ordinal)
            || !ValidEmail(expectedCurrentEmail) || !ValidEmail(newEmail))
            throw new InvalidOperationException("Confirmed email change is unavailable.");

        var token = await GetManagementTokenAsync(baseUrl, cancellationToken);
        var url = $"{baseUrl}/api/v2/users/{Uri.EscapeDataString(user.ExternalSubject)}";
        var current = await ReadConfirmedUserAsync(url, token, user.ExternalSubject, connection, cancellationToken);
        if (SameEmail(current.Email, newEmail) && current.Verified) return;
        if (!SameEmail(current.Email, expectedCurrentEmail))
            throw new InvalidOperationException("Confirmed email change could not be reconciled.");

        // Mailbox possession was proved by the durable Platform action, before this call.
        // Sending verify_email=true here would change the identity before that proof instead.
        using var request = new HttpRequestMessage(HttpMethod.Patch, url);
        request.Headers.Authorization = new("Bearer", token);
        request.Content = JsonContent.Create(new { email = newEmail, email_verified = true, verify_email = false, connection });
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Confirmed email change could not be completed.");
        var updated = await ReadConfirmedUserAsync(url, token, user.ExternalSubject, connection, cancellationToken);
        if (!SameEmail(updated.Email, newEmail) || !updated.Verified)
            throw new InvalidOperationException("Confirmed email change could not be reconciled.");
    }

    private async Task<(string Email, bool Verified)> ReadConfirmedUserAsync(string url, string token,
        string subject, string connection, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new("Bearer", token);
        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Confirmed email change could not be reconciled.");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        if (!body.TryGetProperty("user_id", out var id) || id.GetString() != subject
            || !body.TryGetProperty("email", out var email) || email.ValueKind != JsonValueKind.String
            || !ValidEmail(email.GetString())
            || body.TryGetProperty("blocked", out var blocked) && blocked.ValueKind != JsonValueKind.False
            || !body.TryGetProperty("identities", out var identities) || identities.ValueKind != JsonValueKind.Array
            || identities.GetArrayLength() != 1)
            throw new InvalidOperationException("Confirmed email change is unavailable.");
        var identity = identities[0];
        if (!identity.TryGetProperty("provider", out var provider) || provider.GetString() != "auth0"
            || !identity.TryGetProperty("connection", out var source) || source.GetString() != connection
            || !identity.TryGetProperty("user_id", out var localId) || $"auth0|{localId.GetString()}" != subject
            || identity.TryGetProperty("isSocial", out var social) && social.ValueKind != JsonValueKind.False)
            throw new InvalidOperationException("Confirmed email change is unavailable.");
        return (email.GetString()!, body.TryGetProperty("email_verified", out var verified) && verified.ValueKind == JsonValueKind.True);
    }

    private static bool SameEmail(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    private static bool ValidEmail(string? email) => email is { Length: > 0 and <= 254 }
        && !email.Any(char.IsControl) && System.Net.Mail.MailAddress.TryCreate(email, out var parsed) && parsed.Address == email;

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
            throw new InvalidOperationException("Identity provider authorization is unavailable.");
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
