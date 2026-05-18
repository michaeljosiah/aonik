using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aonik.SharedKernel.Abstractions.Agents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Finance.Agents.CodeAct;

/// <summary>
/// CodeAct sandbox provider backed by Azure Container Apps Dynamic Sessions
/// (<see href="https://learn.microsoft.com/en-us/azure/container-apps/sessions-code-interpreter"/>).
/// </summary>
/// <remarks>
/// <para>
/// On each <see cref="TryBuildExecuteCodeTool"/> call we:
/// </para>
/// <list type="number">
///   <item>Issue an HMAC-signed nonce that binds (runId, sub-agent, tenant, user, tool whitelist, expiry, budget).</item>
///   <item>Build a Python preamble that defines <c>call_tool(name, **kwargs)</c> as a synchronous POST back to our API.</item>
///   <item>Return an <see cref="AIFunction"/> whose invoke handler prepends the preamble to the LLM-generated code and submits the combined script to ACA Sessions' <c>/executions</c> endpoint.</item>
/// </list>
/// <para>
/// The session identifier is derived from <c>HMAC(RunId + SubAgentName)</c> so
/// repeated <c>execute_code</c> invocations in the same sub-agent run hit
/// the same warm Python interpreter (state persists), but never leak across
/// users.
/// </para>
/// </remarks>
public sealed class AcaSessionsCodeActSandboxProvider : ICodeActSandboxProvider
{
    private const string ExecuteCodeToolName = "execute_code";

    private const string ExecuteCodeToolDescription =
        "Execute Python code in a secure isolated Azure Container Apps sandbox. " +
        "Inside the sandbox, call_tool(name, **kwargs) invokes a registered host tool " +
        "by name (synchronously over HTTPS callback). Use this when an analysis needs " +
        "to loop over data, do parametric arithmetic, or compose multiple tool results " +
        "in one pass. The sandbox has no filesystem persistence between runs and no " +
        "network access beyond the call_tool callback.";

    /// <summary>
    /// Bare regex that catches obvious attempts to shadow the bridge.
    /// Not bulletproof (the LLM could obfuscate) but surfaces the typical
    /// mistakes loudly rather than letting the script silently misbehave.
    /// </summary>
    private static readonly Regex CallToolShadowing = new(
        @"(?:^|\W)(?:def\s+call_tool|call_tool\s*=)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>
    /// Cap on the size of the LLM-generated code we forward to ACA. ACA's
    /// own limits are documented as 128 MB request body; 256 KB is more
    /// than enough for the structured-output JSON loops sub-agents emit
    /// and keeps the failure mode explicit.
    /// </summary>
    private const int MaxScriptBytes = 256 * 1024;

    private readonly AcaSessionsClient _client;
    private readonly AcaSessionsOptions _options;
    private readonly CodeActCallbackNonceService _nonceService;
    private readonly ILogger<AcaSessionsCodeActSandboxProvider> _logger;
    private readonly Lazy<byte[]> _sessionIdSecret;

    public AcaSessionsCodeActSandboxProvider(
        AcaSessionsClient client,
        IOptions<AcaSessionsOptions> options,
        CodeActCallbackNonceService nonceService,
        ILogger<AcaSessionsCodeActSandboxProvider> logger,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _client = client;
        _options = options.Value;
        _nonceService = nonceService;
        _logger = logger;

        // Lazy resolution mirrors CodeActCallbackNonceService: DI containers
        // construct this provider eagerly when "AcaSessions" is selected, but
        // we don't want construction-time to fail just because the operator
        // hasn't provided a signing key yet (e.g. local tests, dev with the
        // tool-loop fallback still active). The key is only needed inside
        // TryBuildExecuteCodeTool, which throws clearly when the key is
        // missing. Reuses the nonce signing key — same trust boundary; an
        // extra secret would add operational burden without raising the bar.
        _sessionIdSecret = new Lazy<byte[]>(() =>
        {
            var keyConfig = configuration["Ai:CodeAct:NonceSigningKey"]
                ?? throw new InvalidOperationException(
                    "Ai:CodeAct:NonceSigningKey is required when the AcaSessions provider is enabled.");
            return DecodeKey(keyConfig);
        });
    }

    public AIFunction? TryBuildExecuteCodeTool(
        CodeActSandboxContext context,
        IReadOnlyList<AIFunction> hostTools)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(hostTools);

        if (string.IsNullOrWhiteSpace(_options.PoolManagementEndpoint))
        {
            _logger.LogWarning(
                "AcaSessions CodeAct provider is selected but Ai:CodeAct:AcaSessions:PoolManagementEndpoint is empty. " +
                "Falling through to tool-loop path.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(_options.CallbackBaseUrl))
        {
            _logger.LogWarning(
                "AcaSessions CodeAct provider is selected but Ai:CodeAct:AcaSessions:CallbackBaseUrl is empty. " +
                "Falling through to tool-loop path.");
            return null;
        }

        var allowedToolNames = hostTools.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        var nonce = _nonceService.Issue(
            context,
            allowedToolNames,
            maxCallbacks: _options.MaxCallbacksPerNonce,
            ttl: TimeSpan.FromSeconds(_options.NonceTtlSeconds));

        var callbackUrl = BuildCallbackUrl(nonce);
        var sessionIdentifier = BuildSessionIdentifier(context);
        var preamble = BuildPythonPreamble(callbackUrl);

        var executeCode = AIFunctionFactory.Create(
            method: (
                [Description("The Python source code to execute. The sandbox already defines `call_tool(name, **kwargs)` — do NOT redefine it.")] string code,
                CancellationToken ct) => ExecuteAsync(sessionIdentifier, preamble, code, ct),
            name: ExecuteCodeToolName,
            description: ExecuteCodeToolDescription);

        return executeCode;
    }

    private async Task<string> ExecuteAsync(
        string sessionIdentifier,
        string preamble,
        string code,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return SerializeError("empty_code", "execute_code received empty source.");
        }

        if (CallToolShadowing.IsMatch(code))
        {
            return SerializeError(
                "call_tool_shadowed",
                "Your code redefines or shadows `call_tool`. Use the provided `call_tool(name, **kwargs)` directly — do not assign to it or define a new one.");
        }

        if (Encoding.UTF8.GetByteCount(code) > MaxScriptBytes)
        {
            return SerializeError(
                "code_too_large",
                $"execute_code script exceeded the {MaxScriptBytes / 1024} KB limit. Reduce inline data or split the analysis.");
        }

        var combined = preamble + "\n\n# === BEGIN LLM CODE ===\n" + code;

        try
        {
            var result = await _client.ExecuteAsync(sessionIdentifier, combined, cancellationToken)
                .ConfigureAwait(false);
            return JsonSerializer.Serialize(new
            {
                status = result.Status,
                stdout = result.Stdout,
                stderr = result.Stderr,
                executionTimeMs = result.ExecutionTimeInMilliseconds,
                result = result.Result,
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "ACA Sessions execute failed for session {Session}", sessionIdentifier);
            return SerializeError(
                "aca_sessions_http_error",
                $"ACA Sessions /executions failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected ACA Sessions execute failure for session {Session}", sessionIdentifier);
            return SerializeError(
                "aca_sessions_unexpected_error",
                $"Unexpected error invoking the sandbox: {ex.Message}");
        }
    }

    private static string SerializeError(string code, string message) =>
        JsonSerializer.Serialize(new
        {
            status = "Failure",
            stdout = (string?)null,
            stderr = message,
            error = new { code, message },
        });

    private string BuildCallbackUrl(string nonce)
    {
        var baseUrl = _options.CallbackBaseUrl.TrimEnd('/');
        return $"{baseUrl}/ai/codeact/call-tool/{Uri.EscapeDataString(nonce)}";
    }

    private string BuildSessionIdentifier(CodeActSandboxContext context)
    {
        using var hmac = new HMACSHA256(_sessionIdSecret.Value);
        var seed = $"{context.RunId}:{context.SubAgentName}";
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(seed));
        // ACA Sessions identifiers must use a constrained character set; hex
        // is always safe.
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Builds the Python preamble defining <c>call_tool(name, **kwargs)</c> as
    /// a synchronous POST back to our API. The URL is baked literally because
    /// ACA Sessions doesn't accept per-execution env vars.
    /// </summary>
    private static string BuildPythonPreamble(string callbackUrl)
    {
        // The URL is constructed by us (not LLM-supplied), but JSON-encode
        // anyway so quotes/backslashes can't break out of the Python literal.
        var encodedUrl = JsonSerializer.Serialize(callbackUrl);

        // Built line-by-line with explicit "\n" so we sidestep C# raw-string
        // whitespace-alignment rules (the Python body legitimately contains
        // blank lines and column-0 statements). The host whitelist + nonce
        // budget are enforced server-side regardless of what the LLM-generated
        // code does with the bridge.
        var lines = new[]
        {
            "import json as _json",
            "import urllib.request as _urlreq",
            "import urllib.error as _urlerr",
            "",
            "_CB_URL = " + encodedUrl,
            "",
            "def call_tool(name, **kwargs):",
            "    req = _urlreq.Request(",
            "        _CB_URL,",
            "        data=_json.dumps({\"name\": name, \"args\": kwargs}).encode(\"utf-8\"),",
            "        headers={\"Content-Type\": \"application/json\"},",
            "        method=\"POST\",",
            "    )",
            "    try:",
            "        with _urlreq.urlopen(req, timeout=120) as r:",
            "            return _json.loads(r.read())",
            "    except _urlerr.HTTPError as e:",
            "        body = e.read().decode(\"utf-8\", \"replace\")[:500]",
            "        raise RuntimeError(",
            "            \"call_tool(\" + name + \") failed: HTTP \" + str(e.code) + \" \" + body",
            "        )",
        };
        return string.Join("\n", lines);
    }

    private static byte[] DecodeKey(string raw)
    {
        // Mirror CodeActCallbackNonceService.DecodeKey — trim before parsing
        // so trailing whitespace from GitHub Actions secret copy-paste
        // doesn't break key resolution.
        var trimmed = raw.Trim();
        try { return Convert.FromHexString(trimmed); }
        catch (FormatException) { /* fall through */ }
        try { return Convert.FromBase64String(trimmed); }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "Ai:CodeAct:NonceSigningKey must be hex or base64 encoded " +
                $"(received {trimmed.Length} characters after trimming whitespace).");
        }
    }
}
