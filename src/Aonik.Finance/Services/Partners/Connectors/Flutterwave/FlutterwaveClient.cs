using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Finance.Services.Partners.Connectors.Flutterwave.Dtos;

namespace Aonik.Finance.Services.Partners.Connectors.Flutterwave;

/// <summary>
/// Thin typed wrapper over the Flutterwave v4 HTTP API (Spec 037 §7.3). Builds requests, stamps the
/// per-call <c>X-Idempotency-Key</c> + <c>X-Trace-Id</c> (the connector supplies the key — mutating
/// calls pass a deterministic one, read-like calls a fresh one), deserializes the
/// <c>{ status, message, data }</c> envelope, maps non-2xx to <see cref="FlutterwaveException"/>, and
/// surfaces transport timeouts as <see cref="TimeoutException"/>. <c>Authorization</c> is added by
/// <see cref="FlutterwaveAuthHandler"/> in the pipeline, not here.
/// </summary>
internal sealed class FlutterwaveClient
{
    private readonly HttpClient _httpClient;

    public FlutterwaveClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<TResponse> PostAsync<TResponse>(
        string path, object body, string idempotencyKey, CancellationToken cancellationToken)
        where TResponse : class
        => SendAsync<TResponse>(HttpMethod.Post, path, body, idempotencyKey, cancellationToken);

    public Task<TResponse> GetAsync<TResponse>(string path, CancellationToken cancellationToken)
        where TResponse : class
        => SendAsync<TResponse>(HttpMethod.Get, path, body: null, idempotencyKey: null, cancellationToken);

    private async Task<TResponse> SendAsync<TResponse>(
        HttpMethod method,
        string path,
        object? body,
        string? idempotencyKey,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        using var request = new HttpRequestMessage(method, path);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: FlutterwaveJson.Options);
        }

        // X-Idempotency-Key only on mutating calls (the connector supplies a key for POSTs).
        if (idempotencyKey is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Idempotency-Key", idempotencyKey);
        }

        // X-Trace-Id on EVERY request — Flutterwave v4 expects it on reads too (e.g. the
        // GET /transfers/{id} status poll), so it must not be gated on the idempotency key.
        // Reuse the idempotency key when present (deterministic, alphanumeric 12–255); otherwise a
        // fresh value for GETs (§7.3).
        request.Headers.TryAddWithoutValidation(
            "X-Trace-Id", idempotencyKey ?? FlutterwaveReferences.FreshIdempotencyKey());

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Flutterwave request to '{path}' timed out.", ex);
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
                    $"Flutterwave returned an unparseable payload for '{path}'.",
                    "DESERIALIZE", null, response.StatusCode, retryable: false, ex);
            }

            if (typed is null)
            {
                throw new FlutterwaveException(
                    $"Flutterwave returned an empty payload for '{path}'.",
                    "EMPTY", null, response.StatusCode, retryable: false);
            }

            return typed;
        }
    }

    private static FlutterwaveException CreateException(
        System.Net.HttpStatusCode statusCode, string payload, string path)
    {
        FwError? error = null;
        try
        {
            error = JsonSerializer.Deserialize<FwErrorEnvelope>(payload, FlutterwaveJson.Options)?.Error;
        }
        catch (JsonException)
        {
            // Non-JSON / unexpected error body — fall back to a status-based message.
        }

        var message = error?.Message
            ?? $"Flutterwave request to '{path}' failed with status {(int)statusCode}.";

        return new FlutterwaveException(
            message,
            error?.Type,
            error?.Code,
            statusCode,
            retryable: FlutterwaveException.IsRetryableStatus(statusCode));
    }
}
