using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Services.Settings;

namespace Aonik.Infrastructure.Authentication.PasswordReset;

public class AzureAdB2cPasswordResetService : IIdpPasswordResetService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingProvider _settingProvider;

    public AzureAdB2cPasswordResetService(HttpClient httpClient, ISettingProvider settingProvider)
    {
        _httpClient = httpClient;
        _settingProvider = settingProvider;
    }

    public async Task TriggerResetAsync(string email, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var authority = await _settingProvider.GetRequiredAsync(AuthSettingNames.AzureAdAuthority, cancellationToken);
        var clientId = await _settingProvider.GetRequiredAsync(AuthSettingNames.AzureAdClientId, cancellationToken);

        var authorityBase = authority.TrimEnd('/');
        var resetUrl = $"{authorityBase}/oauth2/v2.0/authorize";

        var query = new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["response_type"] = "code",
            ["scope"] = "openid",
            ["login_hint"] = email,
            ["prompt"] = "login",
            ["redirect_uri"] = "urn:ietf:wg:oauth:2.0:oob",
            ["response_mode"] = "query",
            ["state"] = Guid.NewGuid().ToString("N")
        };

        var uriBuilder = new UriBuilder(resetUrl)
        {
            Query = string.Join("&", query
                .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
                .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value!)}"))
        };

        using var response = await _httpClient.GetAsync(uriBuilder.Uri, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.Redirect)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Azure AD B2C password reset failed: {response.StatusCode} {error}");
        }
    }
}
