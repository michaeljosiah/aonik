using System.Net.Http.Headers;

namespace Aonik.Finance.Services.Partners.Connectors.Flutterwave.Bills;

/// <summary>
/// Stamps <c>Authorization: Bearer {SecretKey}</c> on every outbound v3 Bills request from the static
/// secret key resolved by <see cref="IFlutterwaveBillsConfigProvider"/>. Unlike the v4
/// <c>FlutterwaveAuthHandler</c> there is no token lifecycle or 401-replay — the key is long-lived
/// (Spec 040 §3). When the key is unset the header is omitted; the connector still fails closed before
/// any call because <see cref="FlutterwaveBillsOptions.IsConfigured"/> is false.
/// </summary>
internal sealed class FlutterwaveBillsAuthHandler : DelegatingHandler
{
    private readonly IFlutterwaveBillsConfigProvider _configProvider;

    public FlutterwaveBillsAuthHandler(IFlutterwaveBillsConfigProvider configProvider)
    {
        _configProvider = configProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var options = await _configProvider.GetAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(options.SecretKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.SecretKey);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
