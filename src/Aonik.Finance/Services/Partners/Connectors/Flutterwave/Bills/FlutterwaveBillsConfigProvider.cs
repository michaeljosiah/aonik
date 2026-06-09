using Aonik.SharedKernel.Abstractions.Settings;
using Microsoft.Extensions.Options;

namespace Aonik.Finance.Services.Partners.Connectors.Flutterwave.Bills;

/// <summary>
/// Reads the v3 Bills settings via <see cref="ISettingProvider"/> (DB-backed, scope-aware) with the
/// <c>appsettings</c>-bound <see cref="FlutterwaveBillsOptions"/> as fallback — mirroring the v4
/// <c>FlutterwaveConfigProvider</c>. The secret key is supplied per-deployment via environment /
/// user-secrets and resolved here at runtime.
/// </summary>
internal sealed class FlutterwaveBillsConfigProvider : IFlutterwaveBillsConfigProvider
{
    private readonly ISettingProvider _settingProvider;
    private readonly FlutterwaveBillsOptions _fallback;

    public FlutterwaveBillsConfigProvider(
        ISettingProvider settingProvider,
        IOptions<FlutterwaveBillsOptions> fallback)
    {
        _settingProvider = settingProvider;
        _fallback = fallback.Value;
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
