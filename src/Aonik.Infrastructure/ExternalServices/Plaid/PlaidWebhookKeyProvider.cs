using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Aonik.Infrastructure.ExternalServices.Plaid;

/// <summary>
/// Fetches Plaid's webhook-verification public keys from
/// <c>POST /webhook_verification_key/get</c> and caches them by key id. Plaid rotates these
/// keys, so each webhook's JWS header names the <c>kid</c> that signed it; we resolve and
/// cache that key. The cache keeps the per-request key fetch off the webhook hot path.
/// </summary>
internal sealed class PlaidWebhookKeyProvider : IPlaidWebhookKeyProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly PlaidWebhookVerificationOptions _options;
    private readonly ILogger<PlaidWebhookKeyProvider> _logger;

    public PlaidWebhookKeyProvider(
        HttpClient httpClient,
        IMemoryCache cache,
        IOptions<PlaidWebhookVerificationOptions> options,
        ILogger<PlaidWebhookKeyProvider> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<JsonWebKey?> GetKeyAsync(string keyId, CancellationToken cancellationToken)
    {
        var cacheKey = $"plaid-webhook-key:{keyId}";
        if (_cache.TryGetValue(cacheKey, out JsonWebKey? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "/webhook_verification_key/get",
                new PlaidWebhookKeyRequest(_options.ClientId, _options.Secret, keyId),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Plaid webhook key fetch for kid {Kid} failed with status {StatusCode}.",
                    keyId, (int)response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("key", out var keyElement))
            {
                _logger.LogWarning("Plaid webhook key response for kid {Kid} had no 'key' object.", keyId);
                return null;
            }

            var jwk = new JsonWebKey(keyElement.GetRawText());
            _cache.Set(cacheKey, jwk, CacheDuration);
            return jwk;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            _logger.LogWarning(ex, "Plaid webhook key fetch for kid {Kid} failed.", keyId);
            return null;
        }
    }

    private sealed record PlaidWebhookKeyRequest(string ClientId, string Secret, string KeyId)
    {
        [System.Text.Json.Serialization.JsonPropertyName("client_id")]
        public string ClientId { get; init; } = ClientId;

        [System.Text.Json.Serialization.JsonPropertyName("secret")]
        public string Secret { get; init; } = Secret;

        [System.Text.Json.Serialization.JsonPropertyName("key_id")]
        public string KeyId { get; init; } = KeyId;
    }
}
