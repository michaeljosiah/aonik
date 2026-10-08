using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Options;

using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Services.Fulfilment;

namespace Aonik.Infrastructure.ExternalServices.Postcodes;

internal sealed class PostcodesIoLookup(HttpClient httpClient, IOptions<PostcodesIoOptions> options)
    : IPostcodeLookup
{
    internal const long MaxResponseBytes = 64 * 1024;
    internal static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    public async Task<PostcodeLookupResult> LookupAsync(
        string normalisedPostcode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!options.Value.Enabled)
            return new(PostcodeLookupStatus.Unavailable);

        var postcode = UkPostcode.Normalize(normalisedPostcode);
        if (postcode is null)
            return new(PostcodeLookupStatus.NotFound);

        var compactPostcode = postcode.Replace(" ", string.Empty);
        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"https://api.postcodes.io/postcodes/{Uri.EscapeDataString(compactPostcode)}");

        try
        {
            // ResponseContentRead keeps the entire response inside HttpClient's timeout and buffer limit.
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return new(PostcodeLookupStatus.NotFound);
            if (response.StatusCode != HttpStatusCode.OK)
                return new(PostcodeLookupStatus.Unavailable);

            var payload = await response.Content.ReadFromJsonAsync<LookupResponse>(cancellationToken);
            var foundPostcode = UkPostcode.Normalize(payload?.Result?.Postcode);
            var foundOutwardCode = UkPostcode.NormalizeOutwardCode(payload?.Result?.Outcode);
            if (payload?.Status != 200 || foundPostcode != postcode
                || foundOutwardCode != postcode[..postcode.IndexOf(' ')])
            {
                return new(PostcodeLookupStatus.Unavailable);
            }

            return new(PostcodeLookupStatus.Found, foundPostcode);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(PostcodeLookupStatus.Unavailable);
        }
        catch (HttpRequestException)
        {
            return new(PostcodeLookupStatus.Unavailable);
        }
        catch (IOException)
        {
            return new(PostcodeLookupStatus.Unavailable);
        }
        catch (JsonException)
        {
            return new(PostcodeLookupStatus.Unavailable);
        }
    }

    private sealed record LookupResponse(int Status, LookupPostcode? Result);
    private sealed record LookupPostcode(string? Postcode, string? Outcode);
}
