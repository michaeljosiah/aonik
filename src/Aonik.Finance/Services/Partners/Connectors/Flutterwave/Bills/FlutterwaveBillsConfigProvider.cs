using Aonik.Finance.Services.Partners.Connectors;
using Aonik.Finance.Services.Partners.Connectors.Credentials;
using Aonik.Finance.Services.Partners.Connectors.Registry;
using Aonik.SharedKernel.Abstractions.Settings;
using Microsoft.Extensions.Options;

namespace Aonik.Finance.Services.Partners.Connectors.Flutterwave.Bills;

/// <summary>
/// Resolves v3 Bills options. For a bound connector row the secret key comes from the decrypted bundle and
/// the base URL is derived from the <c>environment</c> config (Spec 042 §8); the legacy
/// <c>Finance.Partners.Flutterwave.Bills.*</c> settings apply only to the migrated default connector.
/// </summary>
internal sealed class FlutterwaveBillsConfigProvider : IFlutterwaveBillsConfigProvider
{
    private readonly ISettingProvider _settingProvider;
    private readonly ICredentialBundleService _bundleService;
    private readonly FlutterwaveBillsOptions _fallback;

    public FlutterwaveBillsConfigProvider(
        ISettingProvider settingProvider,
        ICredentialBundleService bundleService,
        IOptions<FlutterwaveBillsOptions> fallback)
    {
        _settingProvider = settingProvider;
        _bundleService = bundleService;
        _fallback = fallback.Value;
    }

    public async Task<FlutterwaveBillsOptions> GetAsync(ConnectorBinding binding, CancellationToken cancellationToken = default)
    {
        if (binding.HasBundle)
        {
            return await BuildFromBundleAsync(binding, cancellationToken);
        }

        if (binding.AllowLegacyFallback)
        {
            return await GetAsync(cancellationToken);
        }

        throw new FlutterwaveException(
            $"Connector {binding.ConnectorId} ({binding.ConnectorKind}) has no bound credential bundle and "
            + "is not the legacy default; configure its credentials before use.",
            errorType: "CONFIGURATION", errorCode: null, statusCode: null, retryable: false);
    }

    private async Task<FlutterwaveBillsOptions> BuildFromBundleAsync(ConnectorBinding binding, CancellationToken cancellationToken)
    {
        var bundle = await _bundleService.ResolveAsync(binding.CredentialsRef!, cancellationToken)
            ?? throw new FlutterwaveException(
                $"Credential bundle '{binding.CredentialsRef}' for connector {binding.ConnectorId} was not found.",
                errorType: "CONFIGURATION", errorCode: null, statusCode: null, retryable: false);

        var descriptor = ConnectorRegistry.GetRequired(binding.ConnectorKind);
        var config = ConnectorConfigJson.Parse(binding.ConfigJson);
        var environment = config.GetValueOrDefault(ConnectorRegistry.ConfigEnvironment);
        var endpoints = descriptor.ResolveEnvironment(environment);

        return new FlutterwaveBillsOptions
        {
            Enabled = true,
            BaseUrl = endpoints.BaseUrl,
            SecretKey = bundle.Secrets.GetCurrent(ConnectorRegistry.FieldSecretKey) ?? string.Empty,
            Country = config.GetValueOrDefault(ConnectorRegistry.ConfigCountry)
                ?? descriptor.Config(ConnectorRegistry.ConfigCountry)?.DefaultValue
                ?? _fallback.Country,
        };
    }

    public async Task<FlutterwaveBillsOptions> GetAsync(CancellationToken cancellationToken = default)
        => new()
        {
            Enabled = await BoolAsync(
                PartnerGatewaySettingNames.FlutterwaveBillsEnabled,
                _fallback.Enabled,
                cancellationToken),
            BaseUrl = await StringAsync(
                PartnerGatewaySettingNames.FlutterwaveBillsBaseUrl,
                _fallback.BaseUrl,
                cancellationToken),
            SecretKey = await StringAsync(
                PartnerGatewaySettingNames.FlutterwaveBillsSecretKey,
                _fallback.SecretKey,
                cancellationToken),
            Country = _fallback.Country
        };

    private async Task<string> StringAsync(string key, string fallback, CancellationToken cancellationToken)
    {
        var value = await _settingProvider.GetAsync(key, cancellationToken);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private async Task<bool> BoolAsync(string key, bool fallback, CancellationToken cancellationToken)
    {
        var value = await _settingProvider.GetAsync(key, cancellationToken);
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }
}
