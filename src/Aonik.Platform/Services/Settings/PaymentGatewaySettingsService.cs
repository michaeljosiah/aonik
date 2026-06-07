using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Platform.Services.Settings;

internal sealed class PaymentGatewaySettingsService : IPaymentGatewaySettingsService
{
    private const string Flutterwave = "Flutterwave";
    private const string DatabaseSource = "Database";
    private const string ConfigurationSource = "Configuration";
    private const string NoneSource = "None";

    private readonly ISettingProvider _settingProvider;
    private readonly ISettingManager _settingManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public PaymentGatewaySettingsService(
        ISettingProvider settingProvider,
        ISettingManager settingManager,
        IHttpClientFactory httpClientFactory,
        IAuditLogWriter auditLogWriter,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _settingProvider = settingProvider;
        _settingManager = settingManager;
        _httpClientFactory = httpClientFactory;
        _auditLogWriter = auditLogWriter;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<PaymentGatewaySettingsSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        var clientSecret = await _settingProvider.GetAsync(
            PartnerGatewaySettingNames.FlutterwaveClientSecret, cancellationToken);
        var encryptionKey = await _settingProvider.GetAsync(
            PartnerGatewaySettingNames.FlutterwaveEncryptionKey, cancellationToken);
        var signingSecret = await _settingProvider.GetAsync(
            PartnerGatewaySettingNames.FlutterwaveSigningSecret, cancellationToken);

        var snapshot = new PaymentGatewayProviderSnapshot(
            Flutterwave,
            await BoolAsync(PartnerGatewaySettingNames.FlutterwaveEnabled, fallback: false, cancellationToken),
            await StringAsync(PartnerGatewaySettingNames.FlutterwaveBaseUrl, cancellationToken),
            await StringAsync(PartnerGatewaySettingNames.FlutterwaveIdpTokenUrl, cancellationToken),
            await StringAsync(PartnerGatewaySettingNames.FlutterwaveClientId, cancellationToken),
            await StringAsync(PartnerGatewaySettingNames.FlutterwaveDefaultTransferPurpose, cancellationToken),
            !string.IsNullOrWhiteSpace(clientSecret),
            !string.IsNullOrWhiteSpace(encryptionKey),
            !string.IsNullOrWhiteSpace(signingSecret),
            await ResolveSecretSourceAsync(cancellationToken));

        return new PaymentGatewaySettingsSnapshot(new[] { snapshot });
    }

    public async Task<PaymentGatewaySettingsSnapshot> UpdateAsync(
        PaymentGatewaySettingsUpdate update,
        CancellationToken cancellationToken = default)
    {
        foreach (var provider in update.Providers)
        {
            if (!string.Equals(provider.ProviderCode, Flutterwave, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Unsupported payment gateway provider '{provider.ProviderCode}'.");
            }

            await _settingManager.SetAsync(
                PartnerGatewaySettingNames.FlutterwaveEnabled,
                provider.Enabled ? "true" : "false",
                cancellationToken);
            await _settingManager.SetAsync(
                PartnerGatewaySettingNames.FlutterwaveBaseUrl,
                provider.BaseUrl,
                cancellationToken);
            await _settingManager.SetAsync(
                PartnerGatewaySettingNames.FlutterwaveIdpTokenUrl,
                provider.IdpTokenUrl,
                cancellationToken);
            await _settingManager.SetAsync(
                PartnerGatewaySettingNames.FlutterwaveClientId,
                provider.ClientId,
                cancellationToken);
            await _settingManager.SetAsync(
                PartnerGatewaySettingNames.FlutterwaveDefaultTransferPurpose,
                provider.DefaultTransferPurpose,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(provider.ClientSecret))
            {
                await _settingManager.SetAsync(
                    PartnerGatewaySettingNames.FlutterwaveClientSecret,
                    provider.ClientSecret,
                    cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(provider.EncryptionKey))
            {
                await _settingManager.SetAsync(
                    PartnerGatewaySettingNames.FlutterwaveEncryptionKey,
                    provider.EncryptionKey,
                    cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(provider.SigningSecret))
            {
                await _settingManager.SetAsync(
                    PartnerGatewaySettingNames.FlutterwaveSigningSecret,
                    provider.SigningSecret,
                    cancellationToken);
            }

            await LogUpdateAsync(provider, cancellationToken);
        }

        return await GetAsync(cancellationToken);
    }

    public async Task<PaymentGatewayTestResult> TestAsync(
        string providerCode,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(providerCode, Flutterwave, StringComparison.OrdinalIgnoreCase))
        {
            return new PaymentGatewayTestResult(false, providerCode, "Unsupported payment gateway provider.");
        }

        var enabled = await BoolAsync(PartnerGatewaySettingNames.FlutterwaveEnabled, fallback: false, cancellationToken);
        if (!enabled)
        {
            return new PaymentGatewayTestResult(false, Flutterwave, "Flutterwave gateway is disabled.");
        }

        var clientId = await StringAsync(PartnerGatewaySettingNames.FlutterwaveClientId, cancellationToken);
        var clientSecret = await _settingProvider.GetAsync(
            PartnerGatewaySettingNames.FlutterwaveClientSecret, cancellationToken);
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return new PaymentGatewayTestResult(false, Flutterwave, "Flutterwave client id/secret are required.");
        }

        try
        {
            var idpTokenUrl = await StringAsync(PartnerGatewaySettingNames.FlutterwaveIdpTokenUrl, cancellationToken);
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(idpTokenUrl, UriKind.Absolute);
            using var response = await client.PostAsync(
                string.Empty,
                new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                    new KeyValuePair<string, string>("client_id", clientId.Trim()),
                    new KeyValuePair<string, string>("client_secret", clientSecret.Trim()),
                }),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new PaymentGatewayTestResult(
                    false,
                    Flutterwave,
                    $"Flutterwave OAuth token request failed with status {(int)response.StatusCode}.");
            }

            var token = await response.Content.ReadFromJsonAsync<FlutterwaveTokenTestResponse>(
                cancellationToken: cancellationToken);
            return string.IsNullOrWhiteSpace(token?.AccessToken)
                ? new PaymentGatewayTestResult(false, Flutterwave, "Flutterwave token response had no access_token.")
                : new PaymentGatewayTestResult(true, Flutterwave, null);
        }
        catch (Exception ex)
        {
            return new PaymentGatewayTestResult(false, Flutterwave, ex.Message);
        }
    }

    private async Task<string> StringAsync(string key, CancellationToken cancellationToken)
        => await _settingProvider.GetAsync(key, cancellationToken) ?? string.Empty;

    private async Task<bool> BoolAsync(string key, bool fallback, CancellationToken cancellationToken)
    {
        var value = await _settingProvider.GetAsync(key, cancellationToken);
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private async Task<string> ResolveSecretSourceAsync(CancellationToken cancellationToken)
    {
        if (await _settingManager.HasStoredValueAsync(
            PartnerGatewaySettingNames.FlutterwaveClientSecret,
            SettingScope.Global,
            cancellationToken: cancellationToken))
        {
            return DatabaseSource;
        }

        var resolved = await _settingProvider.GetResolvedAsync(
            PartnerGatewaySettingNames.FlutterwaveClientSecret,
            cancellationToken: cancellationToken);
        return resolved.Source == ConfigurationSource ? ConfigurationSource : NoneSource;
    }

    private async Task LogUpdateAsync(PaymentGatewayProviderUpdate provider, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TryGetCurrentTenantId(out var resolvedTenantId)
            ? resolvedTenantId
            : Guid.Empty;
        var actorId = _currentUserProvider.GetCurrentUserId();
        var changed = new List<string>
        {
            PartnerGatewaySettingNames.FlutterwaveEnabled,
            PartnerGatewaySettingNames.FlutterwaveBaseUrl,
            PartnerGatewaySettingNames.FlutterwaveIdpTokenUrl,
            PartnerGatewaySettingNames.FlutterwaveClientId,
            PartnerGatewaySettingNames.FlutterwaveDefaultTransferPurpose
        };

        if (!string.IsNullOrWhiteSpace(provider.ClientSecret))
        {
            changed.Add(PartnerGatewaySettingNames.FlutterwaveClientSecret);
        }

        if (!string.IsNullOrWhiteSpace(provider.EncryptionKey))
        {
            changed.Add(PartnerGatewaySettingNames.FlutterwaveEncryptionKey);
        }

        if (!string.IsNullOrWhiteSpace(provider.SigningSecret))
        {
            changed.Add(PartnerGatewaySettingNames.FlutterwaveSigningSecret);
        }

        var detailsJson = JsonSerializer.Serialize(new
        {
            providerCode = provider.ProviderCode,
            changedKeys = changed
        });

        await _auditLogWriter.LogAsync(
            AuditEventNames.GatewaySettingsUpdated,
            "PaymentGatewaySettings",
            Guid.Empty,
            tenantId,
            actorId,
            correlationId: null,
            detailsJson,
            cancellationToken);
    }

    private sealed record FlutterwaveTokenTestResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken);
}
