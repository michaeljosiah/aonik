using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Finance.Services.Partners.Connectors.Flutterwave.Dtos;

namespace Aonik.Finance.Services.Partners.Connectors.Flutterwave.Bills;

/// <summary>
/// Thin typed wrapper over the Flutterwave <strong>v3</strong> Bills HTTP API (Spec 040 §5). Distinct
/// from the v4 <c>FlutterwaveClient</c>: a different base URL (<c>https://api.flutterwave.com/v3</c>) and
/// static secret-key auth (stamped by <see cref="FlutterwaveBillsAuthHandler"/> in the pipeline), with
/// no idempotency-key / trace-id headers. It reuses the shared <c>{ status, message, data }</c> envelope
/// (<see cref="FwEnvelope{T}"/>) and JSON options, maps non-2xx to <see cref="FlutterwaveException"/>,
/// and surfaces transport timeouts as <see cref="TimeoutException"/>. Requests are built as absolute
/// URIs (base + path) to avoid the <c>BaseAddress</c> trailing-slash gotcha that would drop the
/// <c>/v3</c> segment.
/// </summary>
internal sealed class FlutterwaveBillsClient
{
    private readonly HttpClient _httpClient;
    private readonly IFlutterwaveBillsConfigProvider _configProvider;

    public FlutterwaveBillsClient(HttpClient httpClient, IFlutterwaveBillsConfigProvider configProvider)
    {
        _httpClient = httpClient;
        _configProvider = configProvider;
    }

    public Task<TResponse> GetAsync<TResponse>(string path, CancellationToken cancellationToken)
        where TResponse : class
        => SendAsync<TResponse>(HttpMethod.Get, path, body: null, cancellationToken);

    public Task<TResponse> PostAsync<TResponse>(string path, object body, CancellationToken cancellationToken)
        where TResponse : class
        => SendAsync<TResponse>(HttpMethod.Post, path, body, cancellationToken);

    private async Task<TResponse> SendAsync<TResponse>(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        var options = await _configProvider.GetAsync(cancellationToken);
        var baseUrl = options.BaseUrl.TrimEnd('/');
        var uri = new Uri($"{baseUrl}/{path.TrimStart('/')}", UriKind.Absolute);

        using var request = new HttpRequestMessage(method, uri);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: FlutterwaveJson.Options);
        }

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Flutterwave bills request to '{path}' timed out.", ex);
        }

        using (response)
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw CreateException(response.StatusCode, payload, path);
            }

            TResponse? typed;
            try
            {
                typed = JsonSerializer.Deserialize<TResponse>(payload, FlutterwaveJson.Options);
            }
            catch (JsonException ex)
            {
                throw new FlutterwaveException(
                    $"Flutterwave bills returned an unparseable payload for '{path}'.",
                    "DESERIALIZE", null, response.StatusCode, retryable: false, ex);
            }

            if (typed is null)
            {
                throw new FlutterwaveException(
                    $"Flutterwave bills returned an empty payload for '{path}'.",
                    "EMPTY", null, response.StatusCode, retryable: false);
            }

            return typed;
        }
    }

    private static FlutterwaveException CreateException(HttpStatusCode statusCode, string payload, string path)
    {
        // v3 error bodies are a flat { status: "error", message: "…" } — read the message off the
        // shared envelope; fall back to a status-based message when the body is not JSON.
        string? message = null;
        try
        {
            message = JsonSerializer.Deserialize<FwEnvelope<object>>(payload, FlutterwaveJson.Options)?.Message;
        }
        catch (JsonException)
        {
            // Non-JSON / unexpected error body — fall back below.
        }

        return new FlutterwaveException(
            string.IsNullOrWhiteSpace(message)
                ? $"Flutterwave bills request to '{path}' failed with status {(int)statusCode}."
                : message,
            "BILLS",
            null,
            statusCode,
            retryable: FlutterwaveException.IsRetryableStatus(statusCode));
    }
}
