using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Finance.Services.Partners.Connectors.Flutterwave.Dtos;

namespace Aonik.Finance.Services.Partners.Connectors.Flutterwave;

/// <summary>
/// Acquires and caches a Flutterwave v4 OAuth 2.0 client-credentials access token (Spec 037 §7.2,
/// G4). Tokens live 10 minutes; the cache is refreshed ~60s before expiry. Thread-safe via a
/// <see cref="SemaphoreSlim"/> with a double-checked fast path. Resolves its HTTP client through the
/// named <c>flutterwave-idp</c> client (which has no auth handler — that handler calls this provider,
/// so using it here would recurse).
/// </summary>
internal sealed class FlutterwaveTokenProvider
{
    internal const string IdpClientName = "flutterwave-idp";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFlutterwaveConfigProvider _configProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _token;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;
    private string? _cacheKey;

    public FlutterwaveTokenProvider(
        IHttpClientFactory httpClientFactory,
        IFlutterwaveConfigProvider configProvider)
    {
        _httpClientFactory = httpClientFactory;
        _configProvider = configProvider;
    }

    /// <summary>Clock seam — defaults to the system clock; tests override via object initializer.</summary>
    internal TimeProvider Clock { get; init; } = TimeProvider.System;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken, bool forceRefresh = false)
    {
        var options = await _configProvider.GetAsync(cancellationToken);
        EnsureConfigured(options);
        var cacheKey = $"{options.IdpTokenUrl}|{options.ClientId}|{options.ClientSecret}";

        if (!forceRefresh && TryGetCached(cacheKey, out var cached))
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh && TryGetCached(cacheKey, out var cachedAfterWait))
            {
                return cachedAfterWait;
            }

            return await FetchTokenAsync(options, cacheKey, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool TryGetCached(string cacheKey, out string token)
    {
        if (_token is not null && _cacheKey == cacheKey && Clock.GetUtcNow() < _expiresAt)
        {
            token = _token;
            return true;
        }

        token = string.Empty;
        return false;
    }

    private async Task<string> FetchTokenAsync(
        FlutterwaveOptions options,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(IdpClientName);
        client.BaseAddress = new Uri(options.IdpTokenUrl, UriKind.Absolute);

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", options.ClientId.Trim()),
            new KeyValuePair<string, string>("client_secret", options.ClientSecret.Trim()),
        });

        using var response = await client.PostAsync(string.Empty, form, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new FlutterwaveException(
                $"Flutterwave OAuth token request failed with status {(int)response.StatusCode}.",
                errorType: "OAUTH",
                errorCode: null,
                statusCode: response.StatusCode,
                retryable: FlutterwaveException.IsRetryableStatus(response.StatusCode));
        }

        FwTokenResponse? token;
        try
        {
            token = JsonSerializer.Deserialize<FwTokenResponse>(payload, FlutterwaveJson.Options);
        }
        catch (JsonException ex)
        {
            throw new FlutterwaveException(
                "Flutterwave OAuth token response was not valid JSON.",
                "OAUTH", null, response.StatusCode, retryable: false, ex);
        }

        if (string.IsNullOrWhiteSpace(token?.AccessToken))
        {
            throw new FlutterwaveException(
                "Flutterwave OAuth token response did not contain an access_token.",
                "OAUTH", null, response.StatusCode, retryable: false);
        }

        // Refresh at least 60s before the documented expiry; clamp tiny/zero lifetimes.
        var lifetime = Math.Max(token.ExpiresIn, 60);
        _expiresAt = Clock.GetUtcNow().AddSeconds(lifetime - 60);
        _token = token.AccessToken;
        _cacheKey = cacheKey;
        return _token;
    }

    private static void EnsureConfigured(FlutterwaveOptions options)
    {
        if (!options.IsConfigured())
        {
            throw new FlutterwaveException(
                "Flutterwave is not configured or disabled.",
                errorType: "CONFIGURATION",
                errorCode: null,
                statusCode: null,
                retryable: false);
        }
    }
}
