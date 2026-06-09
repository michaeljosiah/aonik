using Aonik.Finance.Services.Partners.Connectors;
using Aonik.Finance.Services.Partners.Connectors.Credentials;
using Aonik.Finance.Services.Partners.Connectors.Registry;
using Aonik.SharedKernel.Abstractions.Settings;
using Microsoft.Extensions.Options;

namespace Aonik.Finance.Services.Partners.Connectors.Flutterwave;

internal sealed class FlutterwaveConfigProvider : IFlutterwaveConfigProvider
{
    private readonly ISettingProvider _settingProvider;
    private readonly ICredentialBundleService _bundleService;
    private readonly FlutterwaveOptions _fallback;

    public FlutterwaveConfigProvider(
        ISettingProvider settingProvider,
        ICredentialBundleService bundleService,
        IOptions<FlutterwaveOptions> fallback)
    {
        _settingProvider = settingProvider;
        _bundleService = bundleService;
        _fallback = fallback.Value;
    }

    public async Task<FlutterwaveOptions> GetAsync(ConnectorBinding binding, CancellationToken cancellationToken = default)
    {
        if (binding.HasBundle)
        {
            return await BuildFromBundleAsync(binding, cancellationToken);
        }

        if (binding.AllowLegacyFallback)
        {
            return await GetAsync(cancellationToken);
        }

        // Fail closed (Spec 042 §7.2): a non-default connector with no bound bundle does NOT borrow the
        // global account — that would route money through the wrong credentials.
        throw new FlutterwaveException(
            $"Connector {binding.ConnectorId} ({binding.ConnectorKind}) has no bound credential bundle and "
            + "is not the legacy default; configure its credentials before use.",
            errorType: "CONFIGURATION", errorCode: null, statusCode: null, retryable: false);
    }

    private async Task<FlutterwaveOptions> BuildFromBundleAsync(ConnectorBinding binding, CancellationToken cancellationToken)
    {
        var bundle = await _bundleService.ResolveAsync(binding.CredentialsRef!, cancellationToken)
            ?? throw new FlutterwaveException(
                $"Credential bundle '{binding.CredentialsRef}' for connector {binding.ConnectorId} was not found.",
                errorType: "CONFIGURATION", errorCode: null, statusCode: null, retryable: false);

        var descriptor = ConnectorRegistry.GetRequired(binding.ConnectorKind);
        var config = ConnectorConfigJson.Parse(binding.ConfigJson);
        var environment = config.GetValueOrDefault(ConnectorRegistry.ConfigEnvironment);
        var endpoints = descriptor.ResolveEnvironment(environment);

        return new FlutterwaveOptions
        {
            // A bound bundle means the connector is configured-by-construction; the runtime IsConfigured()
            // check still guards the required secrets below.
            UseRealFlutterwaveApi = true,
            BaseUrl = endpoints.BaseUrl,
            IdpTokenUrl = endpoints.IdpTokenUrl ?? string.Empty,
            ClientId = bundle.Secrets.GetCurrent(ConnectorRegistry.FieldClientId) ?? string.Empty,
            ClientSecret = bundle.Secrets.GetCurrent(ConnectorRegistry.FieldClientSecret) ?? string.Empty,
            EncryptionKey = bundle.Secrets.GetCurrent(ConnectorRegistry.FieldEncryptionKey) ?? string.Empty,
            DefaultTransferPurpose = config.GetValueOrDefault(ConnectorRegistry.ConfigDefaultTransferPurpose)
                ?? descriptor.Config(ConnectorRegistry.ConfigDefaultTransferPurpose)?.DefaultValue
                ?? _fallback.DefaultTransferPurpose,
        };
    }

    public async Task<FlutterwaveOptions> GetAsync(CancellationToken cancellationToken = default)
        => new()
        {
            UseRealFlutterwaveApi = await BoolAsync(
                PartnerGatewaySettingNames.FlutterwaveEnabled,
                _fallback.UseRealFlutterwaveApi,
                cancellationToken),
            BaseUrl = await StringAsync(
                PartnerGatewaySettingNames.FlutterwaveBaseUrl,
                _fallback.BaseUrl,
                cancellationToken),
            IdpTokenUrl = await StringAsync(
                PartnerGatewaySettingNames.FlutterwaveIdpTokenUrl,
                _fallback.IdpTokenUrl,
                cancellationToken),
            ClientId = await StringAsync(
                PartnerGatewaySettingNames.FlutterwaveClientId,
                _fallback.ClientId,
                cancellationToken),
            ClientSecret = await StringAsync(
                PartnerGatewaySettingNames.FlutterwaveClientSecret,
                _fallback.ClientSecret,
                cancellationToken),
            EncryptionKey = await StringAsync(
                PartnerGatewaySettingNames.FlutterwaveEncryptionKey,
                _fallback.EncryptionKey,
                cancellationToken),
            DefaultTransferPurpose = await StringAsync(
                PartnerGatewaySettingNames.FlutterwaveDefaultTransferPurpose,
                _fallback.DefaultTransferPurpose,
                cancellationToken)
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
