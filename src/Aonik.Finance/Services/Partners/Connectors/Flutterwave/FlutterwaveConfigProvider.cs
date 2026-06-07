using Aonik.SharedKernel.Abstractions.Settings;
using Microsoft.Extensions.Options;

namespace Aonik.Finance.Services.Partners.Connectors.Flutterwave;

internal sealed class FlutterwaveConfigProvider : IFlutterwaveConfigProvider
{
    private readonly ISettingProvider _settingProvider;
    private readonly FlutterwaveOptions _fallback;

    public FlutterwaveConfigProvider(
        ISettingProvider settingProvider,
        IOptions<FlutterwaveOptions> fallback)
    {
        _settingProvider = settingProvider;
        _fallback = fallback.Value;
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
