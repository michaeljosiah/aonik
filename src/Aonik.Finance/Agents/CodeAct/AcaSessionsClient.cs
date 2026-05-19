using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Finance.Agents.CodeAct;

/// <summary>
/// Typed <see cref="HttpClient"/> wrapper over the Azure Container Apps
/// Dynamic Sessions code-execution endpoint. Acquires Microsoft Entra
/// tokens via <see cref="ManagedIdentityCredential"/> with the
/// <c>https://dynamicsessions.io/.default</c> scope, caches them across
/// requests, and retries once with a forced refresh on a 401.
/// </summary>
/// <remarks>
/// Pool-pinned: the base address is the pool management endpoint (with a
/// mandatory trailing "/" — see <c>FinanceModule</c>), so this client
/// targets a single pool. The session identifier is supplied per request
/// via <see cref="ExecuteAsync"/>.
/// </remarks>
public sealed class AcaSessionsClient
{
    private static readonly string[] TokenScopes = ["https://dynamicsessions.io/.default"];

    private static readonly JsonSerializerOptions ResponseJson = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly AcaSessionsOptions _options;
    private readonly ILogger<AcaSessionsClient> _logger;
    private readonly TokenCredential _credential;

    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private AccessToken _cachedToken;

    public AcaSessionsClient(
        HttpClient httpClient,
        IOptions<AcaSessionsOptions> options,
        ILogger<AcaSessionsClient> logger,
        TokenCredential? credentialOverride = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        // Pick which managed identity hits the ACA Sessions /code/execute endpoint.
        //
        // When AI__CODEACT__ACASESSIONS__MANAGEDIDENTITYCLIENTID is set, use
        // that specific user-assigned identity. Otherwise default to the
        // system-assigned identity. Both flavours are accepted by ACA Sessions
        // as long as the calling principal holds Session Executor + Contributor
        // on the pool (see modules/sessions.bicep); the bicep stack pins us to
        // the user-assigned identity (apiPullIdentity) to match Microsoft's
        // dynamic-sessions samples and avoid identity-selection ambiguity when
        // the container has both flavours attached.
        //
        // For local dev / tests, the caller passes credentialOverride.
        if (credentialOverride is not null)
        {
            _credential = credentialOverride;
        }
        else if (!string.IsNullOrWhiteSpace(_options.ManagedIdentityClientId))
        {
            _credential = new ManagedIdentityCredential(_options.ManagedIdentityClientId);
        }
        else
        {
            _credential = new ManagedIdentityCredential();
        }
    }

    /// <summary>
    /// Submits <paramref name="code"/> to a synchronous execution against the
    /// session identified by <paramref name="sessionIdentifier"/>. Throws
    /// <see cref="HttpRequestException"/> on non-success status codes after
    /// at most one auth retry.
    /// </summary>
    public async Task<AcaSessionsExecutionResult> ExecuteAsync(
        string sessionIdentifier,
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        // PythonLTS data-plane path. `/code/execute` + 2024-02-02-preview is
        // the combo LangChain's azure-dynamic-sessions uses; the newer
        // `/executions` endpoint (api-version ≥ 2024-10-02-preview) also
        // works but expects a different request-body shape than the one
        // built below.
        //
        // The path MUST NOT start with "/". BaseAddress is the per-pool
        // management endpoint (.../sessionPools/<name>/) registered in
        // FinanceModule, and per RFC 3986 a leading "/" is an absolute-path
        // reference that discards the base URI's path — every call would
        // then hit https://<region>.dynamicsessions.io/code/execute with no
        // resource context and return HTTP 401.
        var path = $"code/execute?api-version={Uri.EscapeDataString(_options.DataPlaneApiVersion)}" +
                   $"&identifier={Uri.EscapeDataString(sessionIdentifier)}";

        var body = new AcaSessionsExecutionRequest(new AcaSessionsExecutionProperties(
            CodeInputType: "inline",
            ExecutionType: "synchronous",
            Code: code));

        var response = await SendWithAuthAsync(path, body, cancellationToken).ConfigureAwait(false);

        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            // Capture WWW-Authenticate + other auth-relevant headers — ACA's
            // 401 body is empty so the only signal about WHY the token was
            // rejected lives in the headers.
            var wwwAuth = response.Headers.TryGetValues("WWW-Authenticate", out var w) ? string.Join(" | ", w) : "(none)";
            var miseCorrelation = response.Headers.TryGetValues("mise-correlation-id", out var m) ? string.Join(",", m) : "(none)";
            var headerSummary = $"WWW-Authenticate={wwwAuth} | mise-correlation-id={miseCorrelation}";
            LastResponseHeadersForDiagnostic = headerSummary;
            _logger.LogWarning(
                "ACA Sessions /code/execute returned {Status} for session {Session}: body={Body} headers={Headers}",
                (int)response.StatusCode, sessionIdentifier, Truncate(raw, 500), headerSummary);
            throw new HttpRequestException(
                $"ACA Sessions /code/execute failed: HTTP {(int)response.StatusCode} body=\"{Truncate(raw, 500)}\" {headerSummary}",
                inner: null,
                statusCode: response.StatusCode);
        }

        var parsed = JsonSerializer.Deserialize<AcaSessionsExecutionEnvelope>(raw, ResponseJson)
            ?? throw new InvalidOperationException("ACA Sessions returned an empty response body.");

        return parsed.Properties ?? new AcaSessionsExecutionResult(
            Status: "unknown",
            Stdout: null,
            Stderr: null,
            Result: null,
            ExecutionTimeInMilliseconds: null);
    }

    private async Task<HttpResponseMessage> SendWithAuthAsync<TBody>(
        string path,
        TBody body,
        CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);
        LastTokenClaimsForDiagnostic = DecodeJwtClaimsSafe(token);
        var response = await SendAsync(path, body, token, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            token = await GetTokenAsync(forceRefresh: true, cancellationToken).ConfigureAwait(false);
            LastTokenClaimsForDiagnostic = DecodeJwtClaimsSafe(token);
            response = await SendAsync(path, body, token, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    /// <summary>
    /// Last decoded JWT claims (subset: aud / oid / iss / appid / exp) of the
    /// token used to call ACA Sessions. Static, single-slot, populated on every
    /// call so the diagnostic endpoint can reveal whether the token's identity
    /// matches the principalId the Session Executor role was granted to.
    /// Never includes the signature — safe to expose to admin callers.
    /// </summary>
    public static string? LastTokenClaimsForDiagnostic { get; private set; }

    /// <summary>
    /// Last response's auth-relevant headers (WWW-Authenticate, correlation-id)
    /// captured on non-success status codes. ACA's 401 body is empty so
    /// headers are the only signal about WHY the token was rejected.
    /// </summary>
    public static string? LastResponseHeadersForDiagnostic { get; private set; }

    private static string DecodeJwtClaimsSafe(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2) return "(jwt missing payload segment)";
            var padded = parts[1].Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4) { case 2: padded += "=="; break; case 3: padded += "="; break; }
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            // Only surface the subset of claims that matter for RBAC diagnosis.
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            string Get(string name) => root.TryGetProperty(name, out var v) ? v.ToString() : "(missing)";
            return $"aud={Get("aud")} | iss={Get("iss")} | oid={Get("oid")} | appid={Get("appid")} | exp={Get("exp")} | idtyp={Get("idtyp")} | ver={Get("ver")} | scp={Get("scp")} | roles={Get("roles")} | appidacr={Get("appidacr")} | tid={Get("tid")}";
        }
        catch (Exception ex)
        {
            return $"(decode failed: {ex.GetType().Name}: {ex.Message})";
        }
    }

    private async Task<HttpResponseMessage> SendAsync<TBody>(
        string path,
        TBody body,
        string token,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GetTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (!forceRefresh && _cachedToken.Token is not null && _cachedToken.ExpiresOn > now.AddMinutes(5))
        {
            return _cachedToken.Token;
        }

        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!forceRefresh && _cachedToken.Token is not null && _cachedToken.ExpiresOn > now.AddMinutes(5))
            {
                return _cachedToken.Token;
            }

            var ctx = new TokenRequestContext(TokenScopes);
            _cachedToken = await _credential.GetTokenAsync(ctx, cancellationToken).ConfigureAwait(false);
            return _cachedToken.Token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s.Substring(0, max) + "…";

    // ── Wire types ──────────────────────────────────────────────────────────

    private sealed record AcaSessionsExecutionRequest(
        [property: JsonPropertyName("properties")] AcaSessionsExecutionProperties Properties);

    private sealed record AcaSessionsExecutionProperties(
        [property: JsonPropertyName("codeInputType")] string CodeInputType,
        [property: JsonPropertyName("executionType")] string ExecutionType,
        [property: JsonPropertyName("code")] string Code);

    private sealed record AcaSessionsExecutionEnvelope(
        [property: JsonPropertyName("properties")] AcaSessionsExecutionResult? Properties);
}

/// <summary>
/// Decoded result of a synchronous execution. <c>Result</c> is the value of
/// the final expression in the executed code (LLM-style). <c>Stdout</c> is
/// what <c>print(...)</c> wrote.
/// </summary>
public sealed record AcaSessionsExecutionResult(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("stdout")] string? Stdout,
    [property: JsonPropertyName("stderr")] string? Stderr,
    [property: JsonPropertyName("result")] JsonElement? Result,
    [property: JsonPropertyName("executionTimeInMilliseconds")] long? ExecutionTimeInMilliseconds);
