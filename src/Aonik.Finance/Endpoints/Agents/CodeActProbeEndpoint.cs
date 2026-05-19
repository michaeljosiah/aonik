using Aonik.Finance.Agents.CodeAct;
using Azure.Core;
using Azure.Identity;
using FastEndpoints;
using Microsoft.Extensions.Options;

namespace Aonik.Finance.Endpoints.Agents;

/// <summary>
/// Self-contained probe of the AcaSessions stack. First runs the production
/// <see cref="AcaSessionsClient.ExecuteAsync"/> path (so the static
/// diagnostic slots reflect a real call, sidestepping the multi-replica
/// quirk that broke /debug-tail), then sweeps a (path × api-version) matrix
/// directly against the pool. Originally written to isolate which combo
/// accepts the managed-identity token; retained as a regression check —
/// if Microsoft retires our pinned combo or changes the auth model, the
/// matrix immediately surfaces the next working alternative.
/// </summary>
/// <remarks>
/// Admin-only. Diagnostic-only — does not mint a nonce or call back.
/// </remarks>
internal sealed class CodeActProbeEndpoint : EndpointWithoutRequest<CodeActProbeResponse>
{
    private static readonly string[] TokenScopes = ["https://dynamicsessions.io/.default"];

    private readonly AcaSessionsClient _client;
    private readonly IOptions<AcaSessionsOptions> _options;
    private readonly IHttpClientFactory _httpClientFactory;

    public CodeActProbeEndpoint(
        AcaSessionsClient client,
        IOptions<AcaSessionsOptions> options,
        IHttpClientFactory httpClientFactory)
    {
        _client = client;
        _options = options;
        _httpClientFactory = httpClientFactory;
    }

    public override void Configure()
    {
        Post("/ai/codeact/probe");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Probe ACA Sessions: production call + (path × api-version) matrix";
            s.Description = "Runs the production AcaSessionsClient.ExecuteAsync against the pool (so executeResult/exceptionMessage reflect what the sub-agents would see), then sweeps every (path × api-version) combo directly with the same managed-identity token. Useful both for confirming the stack is healthy and for diagnosing future regressions if Microsoft changes the data-plane auth model.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var opts = _options.Value;
        var sessionId = "codeact-probe-" + Guid.NewGuid().ToString("N");

        // Production-path call first — populates the static diagnostic
        // slots (last token claims, last response headers) and reports the
        // result the sub-agents would actually see.
        string? execError = null;
        AcaSessionsExecutionResult? result = null;
        try
        {
            result = await _client.ExecuteAsync(
                sessionIdentifier: sessionId,
                code: "print('probe ok')",
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            execError = $"{ex.GetType().Name}: {ex.Message}";
        }

        // Direct matrix probe — bypass AcaSessionsClient so we can vary the
        // path + api-version per request. Re-acquires the token here to make
        // sure the matrix uses the same identity that the production path would.
        TokenCredential credential = !string.IsNullOrWhiteSpace(opts.ManagedIdentityClientId)
            ? new ManagedIdentityCredential(opts.ManagedIdentityClientId)
            : new ManagedIdentityCredential();

        string? matrixToken = null;
        string? matrixTokenError = null;
        try
        {
            var token = await credential.GetTokenAsync(new TokenRequestContext(TokenScopes), ct).ConfigureAwait(false);
            matrixToken = token.Token;
        }
        catch (Exception ex)
        {
            matrixTokenError = $"{ex.GetType().Name}: {ex.Message}";
        }

        var matrix = new List<CodeActProbeMatrixEntry>();
        if (matrixToken is not null && !string.IsNullOrWhiteSpace(opts.PoolManagementEndpoint))
        {
            var paths = new[] { "/code/execute", "/executions" };
            var versions = new[]
            {
                "2024-02-02-preview",
                "2024-08-02-preview",
                "2024-10-02-preview",
                "2025-02-02-preview",
                "2025-10-02-preview",
            };
            var httpClient = _httpClientFactory.CreateClient("acaSessionsProbe");
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            foreach (var path in paths)
            {
                foreach (var version in versions)
                {
                    var url = $"{opts.PoolManagementEndpoint.TrimEnd('/')}{path}?api-version={Uri.EscapeDataString(version)}&identifier=matrix-{Guid.NewGuid():N}";
                    try
                    {
                        using var req = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, url)
                        {
                            Content = new StringContent(
                                "{\"properties\":{\"codeInputType\":\"inline\",\"executionType\":\"synchronous\",\"code\":\"print('hi')\"}}",
                                System.Text.Encoding.UTF8,
                                "application/json"),
                        };
                        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", matrixToken);
                        using var resp = await httpClient.SendAsync(req, ct).ConfigureAwait(false);
                        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                        var wwwAuth = resp.Headers.TryGetValues("WWW-Authenticate", out var w) ? string.Join(" | ", w) : null;
                        var correlation = resp.Headers.TryGetValues("mise-correlation-id", out var m) ? string.Join(",", m) : null;
                        matrix.Add(new CodeActProbeMatrixEntry(
                            Path: path,
                            ApiVersion: version,
                            StatusCode: (int)resp.StatusCode,
                            BodySnippet: body.Length <= 300 ? body : body.Substring(0, 300) + "…",
                            WwwAuthenticate: wwwAuth,
                            MiseCorrelationId: correlation,
                            Error: null));
                    }
                    catch (Exception ex)
                    {
                        matrix.Add(new CodeActProbeMatrixEntry(
                            Path: path,
                            ApiVersion: version,
                            StatusCode: 0,
                            BodySnippet: null,
                            WwwAuthenticate: null,
                            MiseCorrelationId: null,
                            Error: $"{ex.GetType().Name}: {ex.Message}"));
                    }
                }
            }
        }

        var envSnapshot = new Dictionary<string, string?>
        {
            ["AZURE_CLIENT_ID"] = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID"),
            ["AZURE_TENANT_ID"] = Environment.GetEnvironmentVariable("AZURE_TENANT_ID"),
            ["MSI_ENDPOINT"] = Environment.GetEnvironmentVariable("MSI_ENDPOINT"),
            ["IDENTITY_ENDPOINT"] = Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT") is not null ? "[set]" : null,
            ["IDENTITY_HEADER_SET"] = Environment.GetEnvironmentVariable("IDENTITY_HEADER") is not null ? "[set]" : null,
            ["CONTAINER_APP_NAME"] = Environment.GetEnvironmentVariable("CONTAINER_APP_NAME"),
            ["CONTAINER_APP_REVISION"] = Environment.GetEnvironmentVariable("CONTAINER_APP_REVISION"),
            ["AI__CODEACT__ACASESSIONS__MANAGEDIDENTITYCLIENTID"] = opts.ManagedIdentityClientId is { Length: > 0 } ? "[set]" : null,
        };

        await Send.OkAsync(new CodeActProbeResponse(
            SessionIdentifier: sessionId,
            ExecuteResult: result,
            ExceptionMessage: execError,
            LastTokenClaims: AcaSessionsClient.LastTokenClaimsForDiagnostic,
            LastResponseHeaders: AcaSessionsClient.LastResponseHeadersForDiagnostic,
            MatrixTokenError: matrixTokenError,
            Matrix: matrix,
            EnvSnapshot: envSnapshot), ct);
    }
}

public sealed record CodeActProbeResponse(
    string SessionIdentifier,
    AcaSessionsExecutionResult? ExecuteResult,
    string? ExceptionMessage,
    string? LastTokenClaims,
    string? LastResponseHeaders,
    string? MatrixTokenError,
    IReadOnlyList<CodeActProbeMatrixEntry> Matrix,
    Dictionary<string, string?> EnvSnapshot);

public sealed record CodeActProbeMatrixEntry(
    string Path,
    string ApiVersion,
    int StatusCode,
    string? BodySnippet,
    string? WwwAuthenticate,
    string? MiseCorrelationId,
    string? Error);
