using System.Net;
using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Infrastructure.Integrations.Gmail;

internal static class GmailHttp
{
    internal static async Task<JsonElement> JsonAsync(HttpClient client, HttpRequestMessage request, int maxBytes, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) throw Error(response);
            if (response.Content.Headers.ContentLength > maxBytes) throw new ConnectorProviderException("ProviderResponseTooLarge");
            await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var output = new MemoryStream();
            byte[] buffer = new byte[8192];
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (output.Length + read > maxBytes) throw new ConnectorProviderException("ProviderResponseTooLarge");
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            using JsonDocument json = JsonDocument.Parse(output.ToArray(), new() { MaxDepth = 40 });
            return json.RootElement.Clone();
        }
        catch (HttpRequestException) { throw new ConnectorProviderException("ProviderUnavailable", true); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new ConnectorProviderException("ProviderTimeout", true); }
        catch (JsonException) { throw new ConnectorProviderException("ProviderResponseInvalid"); }
    }

    private static ConnectorProviderException Error(HttpResponseMessage response) => response.StatusCode switch
    {
        HttpStatusCode.Unauthorized => new("ReauthorizationRequired"),
        HttpStatusCode.Forbidden => new("ProviderPermissionDenied"),
        HttpStatusCode.NotFound => new("ProviderItemNotFound"),
        HttpStatusCode.TooManyRequests => new("ProviderRateLimited", true, response.Headers.RetryAfter?.Delta
            ?? (response.Headers.RetryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : null)),
        >= HttpStatusCode.InternalServerError => new("ProviderUnavailable", true),
        _ => new("ProviderRequestRejected"),
    };
}
