using System.Net.Http.Json;

using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Services.Settings;

namespace Aonik.Infrastructure.Authentication.PasswordReset;

public class Auth0PasswordResetService : IIdpPasswordResetService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingProvider _settingProvider;

    public Auth0PasswordResetService(HttpClient httpClient, ISettingProvider settingProvider)
    {
        _httpClient = httpClient;
        _settingProvider = settingProvider;
    }

    public async Task TriggerResetAsync(string email, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var domain = await _settingProvider.GetRequiredAsync(AuthSettingNames.Auth0Domain, cancellationToken);
        var clientId = await _settingProvider.GetRequiredAsync(AuthSettingNames.Auth0ClientId, cancellationToken);
        var connection = await _settingProvider.GetRequiredAsync(AuthSettingNames.Auth0Connection, cancellationToken);

        var baseUrl = NormalizeDomain(domain);
        var request = new
        {
            client_id = clientId,
            email,
            connection
        };

        using var response = await _httpClient.PostAsJsonAsync(
            $"{baseUrl}/dbconnections/change_password",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Auth0 password reset failed: {response.StatusCode} {error}");
        }

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
