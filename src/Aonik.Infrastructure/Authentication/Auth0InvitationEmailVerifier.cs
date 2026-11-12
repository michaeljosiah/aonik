using System.Net.Http.Headers;
using System.Text.Json;

namespace Aonik.Infrastructure.Authentication;

/// <summary>Confirms mailbox ownership before an Auth0 identity can claim an invitation.</summary>
internal static class Auth0InvitationEmailVerifier
{
    public const string ClientName = "Auth0InvitationEmail";

    public static async Task<bool> VerifyAsync(HttpClient client, string authority, string bearer,
        string subject, string invitedEmail, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bearer) || !Uri.TryCreate(authority, UriKind.Absolute, out var issuer)
            || issuer.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(issuer.UserInfo)) return false;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(issuer.ToString().TrimEnd('/') + "/userinfo"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return false;
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var profile = document.RootElement;
            return profile.ValueKind == JsonValueKind.Object
                && profile.TryGetProperty("email_verified", out var verified) && verified.ValueKind == JsonValueKind.True
                && profile.TryGetProperty("sub", out var identity) && identity.ValueKind == JsonValueKind.String && identity.GetString() == subject
                && profile.TryGetProperty("email", out var email) && email.ValueKind == JsonValueKind.String
                && string.Equals(email.GetString(), invitedEmail, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            return false;
        }
    }
}
