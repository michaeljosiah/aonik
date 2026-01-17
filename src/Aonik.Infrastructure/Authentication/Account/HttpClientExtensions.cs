using System.Net.Http.Json;

namespace Aonik.Infrastructure.Authentication.Account;

public static class HttpClientExtensions
{
    public static Task<HttpResponseMessage> PatchAsJsonAsync<T>(
        this HttpClient httpClient,
        string requestUri,
        T content,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, requestUri)
        {
            Content = JsonContent.Create(content)
        };

        return httpClient.SendAsync(request, cancellationToken);
    }
}
