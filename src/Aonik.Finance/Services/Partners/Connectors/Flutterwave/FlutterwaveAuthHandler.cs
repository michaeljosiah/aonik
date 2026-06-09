using System.Net;
using System.Net.Http.Headers;

namespace Aonik.Finance.Services.Partners.Connectors.Flutterwave;

/// <summary>
/// Stamps <c>Authorization: Bearer {token}</c> on every outbound request from the bearer token
/// cached by <see cref="FlutterwaveTokenProvider"/> (Spec 037 §7.3). On a <c>401</c> it forces one
/// token refresh and replays the request once — a token usually expires only if the proactive
/// refresh was skipped; a persistent 401 (bad credentials) falls through unchanged. The per-call
/// <c>X-Idempotency-Key</c> / <c>X-Trace-Id</c> headers are added by <see cref="FlutterwaveClient"/>,
/// not here, because they are request-specific.
/// </summary>
internal sealed class FlutterwaveAuthHandler : DelegatingHandler
{
    private readonly FlutterwaveTokenProvider _tokenProvider;

    public FlutterwaveAuthHandler(FlutterwaveTokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Buffer the body so the request can be replayed after a 401.
        if (request.Content is not null)
        {
            await request.Content.LoadIntoBufferAsync();
        }

        // The connector's resolved options ride on the request (Spec 042 §7) so we authenticate with the
        // bound account's credentials. Absent (legacy/unbound callers) the token provider falls back internally.
        var options = request.Options.TryGetValue(FlutterwaveRequestContext.OptionsKey, out var bound) ? bound : null;

        var token = await _tokenProvider.GetAccessTokenAsync(options, cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        var retry = await CloneAsync(request);
        if (options is not null)
        {
            retry.Options.Set(FlutterwaveRequestContext.OptionsKey, options);
        }

        var refreshed = await _tokenProvider.GetAccessTokenAsync(options, cancellationToken, forceRefresh: true);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed);
        return await base.SendAsync(retry, cancellationToken);
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version
        };

        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
