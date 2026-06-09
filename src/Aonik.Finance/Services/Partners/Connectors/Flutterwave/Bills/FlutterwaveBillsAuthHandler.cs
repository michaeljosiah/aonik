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
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // The bound connector's options ride on the request (Spec 042 §7). When the key is unset the header
        // is omitted; the connector still fails closed before any call via IsConfigured().
        if (request.Options.TryGetValue(FlutterwaveBillsRequestContext.OptionsKey, out var options)
            && !string.IsNullOrWhiteSpace(options.SecretKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.SecretKey);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
