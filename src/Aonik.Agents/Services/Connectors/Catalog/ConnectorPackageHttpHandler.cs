using Aonik.Agents.Framework;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Agents.Services.Connectors.Catalog;

/// <summary>Bounds remote protocol responses before parsing and checks every SDK request against current egress policy.</summary>
internal sealed class ConnectorPackageHttpHandler(ITenantEgressAllowList egress) : DelegatingHandler
{
    internal const int MaximumProtocolBytes = 1_048_576;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!egress.IsAllowed(request.RequestUri?.AbsoluteUri, out _))
            throw new ConnectorAccessException("EndpointBlocked", "The reviewed endpoint is no longer allowed.");
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
        try
        {
            if ((int)response.StatusCode is >= 300 and < 400) throw new ConnectorProviderException("RedirectRefused");
            HttpContent original = response.Content;
            if (original.Headers.ContentLength > MaximumProtocolBytes) throw new ConnectorProviderException("ResponseTooLarge");
            await using Stream stream = await original.ReadAsStreamAsync(cancellationToken);
            byte[] bytes = new byte[MaximumProtocolBytes + 1];
            int count = await stream.ReadAtLeastAsync(bytes, bytes.Length, throwOnEndOfStream: false, cancellationToken);
            if (count > MaximumProtocolBytes) throw new ConnectorProviderException("ResponseTooLarge");
            var bounded = new ByteArrayContent(bytes, 0, count);
            foreach (var header in original.Headers) bounded.Headers.TryAddWithoutValidation(header.Key, header.Value);
            response.Content = bounded;
            original.Dispose();
            return response;
        }
        catch { response.Dispose(); throw; }
    }
}
