using System.Text.Json;
using Aonik.PersonalFinance.Agents.CodeAct;
using Aonik.PersonalFinance.Agents.Tools;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Modules;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aonik.PersonalFinance.Endpoints.Agents;

/// <summary>
/// Receives <c>call_tool(name, **kwargs)</c> requests from Python running
/// inside an Azure Container Apps Dynamic Sessions sandbox. The endpoint is
/// anonymous to ASP.NET's auth pipeline — the nonce in the route IS the auth.
/// </summary>
/// <remarks>
/// <para>
/// Pipeline (rejects fast on any failure so a leaked nonce can't be enumerated
/// for tool surface or budget):
/// </para>
/// <list type="number">
///   <item>Decode + signature-validate nonce → 401 on failure.</item>
///   <item>Re-check that the nonce's tenant has the Personal Finance module enabled → 403
///   <c>module.disabled</c> otherwise (Spec 097 §11). The call is anonymous, so the HTTP module gate
///   had no tenant to check; the signed nonce is the first trustworthy tenant this request carries.</item>
///   <item>Consume one unit of the nonce's callback budget → 429 if exhausted.</item>
///   <item>Assert sub-agent name is one of the registered Spec 025 sub-agents → 400 otherwise.</item>
///   <item>Assert requested tool name is in the nonce's whitelist → 403 otherwise.</item>
///   <item>Override <see cref="ITenantContext"/> + <see cref="ICurrentUserContext"/> from the nonce payload — these are scoped per-request so the overrides die with the response.</item>
///   <item>Resolve the matching <c>PersonalFinanceTools.CreateForXxxSubAgent</c> slice from request scope.</item>
///   <item>Invoke the matched <see cref="AIFunction"/> with parsed args. Return its result as JSON.</item>
/// </list>
/// <para>
/// The nonce service is the ONLY thing preventing cross-execution Python state
/// leakage in a shared session pool — see the documentation on
/// <see cref="CodeActCallbackNonceService"/>.
/// </para>
/// </remarks>
public sealed class CodeActCallbackEndpoint : Endpoint<CodeActCallbackRequest, CodeActCallbackResponse>
{
    private static readonly HashSet<string> ValidSubAgents = new(StringComparer.Ordinal)
    {
        "pf-insights",
        "pf-forecast",
        "pf-classify",
    };

    private readonly CodeActCallbackNonceService _nonceService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IModuleGate _moduleGate;
    private readonly ILogger<CodeActCallbackEndpoint> _logger;

    public CodeActCallbackEndpoint(
        CodeActCallbackNonceService nonceService,
        IHttpContextAccessor httpContextAccessor,
        IModuleGate moduleGate,
        ILogger<CodeActCallbackEndpoint> logger)
    {
        _nonceService = nonceService;
        _httpContextAccessor = httpContextAccessor;
        _moduleGate = moduleGate;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/ai/codeact/call-tool/{Nonce}");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "CodeAct sandbox tool callback";
            s.Description =
                "Authenticated via an HMAC-signed nonce in the route. Invoked by Python " +
                "running inside an Azure Container Apps Dynamic Sessions sandbox via the " +
                "preamble-defined call_tool(name, **kwargs) bridge. Not for human use.";
            s.Response(200, "Tool invoked, result returned");
            s.Response(400, "Invalid sub-agent identifier in nonce payload");
            s.Response(401, "Nonce signature invalid, expired, or replayed");
            s.Response(403, "Requested tool name not in nonce whitelist, or the nonce's tenant has the Personal Finance module disabled (code: module.disabled)");
            s.Response(404, "Tool name does not exist in the resolved sub-agent slice");
            s.Response(429, "Per-nonce callback budget exhausted");
            s.Response(500, "Unexpected error invoking the host tool");
        });
        Options(x => x.WithTags("AI"));
    }

    public override async Task HandleAsync(CodeActCallbackRequest req, CancellationToken ct)
    {
        var validation = _nonceService.Validate(req.Nonce, out var payload, out var signingKeyByteLength);
        if (validation != NonceValidationResult.Valid || payload is null)
        {
            // Length + first-20-chars head only — never echo the full nonce
            // or the signing key. The reason enum tells us which step failed
            // so we can distinguish key-mismatch from expiry vs malformed.
            var nonceLen = req.Nonce?.Length ?? 0;
            var nonceHead = string.IsNullOrEmpty(req.Nonce) ? "" : req.Nonce[..Math.Min(20, req.Nonce.Length)];
            await Send.ResponseAsync(
                new CodeActCallbackResponse(
                    Status: "error",
                    Result: null,
                    Error: new CodeActCallbackError(
                        Code: "nonce_invalid",
                        Message: $"NonceValidationResult={validation} | nonceLen={nonceLen} | nonceHead=\"{nonceHead}\" | signingKeyBytes={signingKeyByteLength}")),
                statusCode: 401,
                cancellation: ct);
            return;
        }

        // The nonce is the first trustworthy tenant this anonymous request carries; the HTTP module gate
        // ran before it was decoded. Refuse before any budget is spent or any tool runs (Spec 097 §11).
        // The bridge's own envelope is kept so the sandbox-side reader sees a typed code, not a bare 403.
        if (!await _moduleGate.IsEnabledAsync(payload.TenantId, ModuleIds.PersonalFinance, ct))
        {
            _logger.LogInformation(
                "CodeAct callback refused: module {ModuleId} is disabled for tenant {TenantId} (run {RunId}).",
                ModuleIds.PersonalFinance, payload.TenantId, payload.RunId);
            await Send.ResponseAsync(
                new CodeActCallbackResponse(
                    Status: "error",
                    Result: null,
                    Error: new CodeActCallbackError(
                        Code: ModuleErrorCodes.Disabled,
                        Message: $"Module '{ModuleIds.PersonalFinance}' is disabled for this tenant.")),
                statusCode: 403,
                cancellation: ct);
            return;
        }

        if (!ValidSubAgents.Contains(payload.SubAgentName))
        {
            await Send.ResponseAsync(
                new CodeActCallbackResponse(
                    Status: "error",
                    Result: null,
                    Error: new CodeActCallbackError(
                        Code: "invalid_subagent",
                        Message: $"Sub-agent '{payload.SubAgentName}' is not a registered CodeAct sub-agent.")),
                statusCode: 400,
                cancellation: ct);
            return;
        }

        if (!payload.ToolWhitelist.Contains(req.Name, StringComparer.Ordinal))
        {
            await Send.ResponseAsync(
                new CodeActCallbackResponse(
                    Status: "error",
                    Result: null,
                    Error: new CodeActCallbackError(
                        Code: "tool_not_in_whitelist",
                        Message: $"Tool '{req.Name}' is not in the nonce's whitelist for sub-agent '{payload.SubAgentName}'.")),
                statusCode: 403,
                cancellation: ct);
            return;
        }

        if (!_nonceService.TryConsumeBudget(payload.Jti))
        {
            await Send.ResponseAsync(
                new CodeActCallbackResponse(
                    Status: "error",
                    Result: null,
                    Error: new CodeActCallbackError(
                        Code: "budget_exhausted",
                        Message: "Per-nonce callback budget exhausted. Reduce the number of call_tool invocations in this execute_code block.")),
                statusCode: 429,
                cancellation: ct);
            return;
        }

        var requestServices = _httpContextAccessor.HttpContext?.RequestServices
            ?? throw new InvalidOperationException("Request scope is required to dispatch a CodeAct callback.");

        // Override the request-scoped multitenancy + current-user state so the
        // resolved tool sees the same scope the parent sub-agent ran under.
        // Both interfaces have settable properties (see ITenantContext.cs and
        // ICurrentUserContext.cs in SharedKernel). The overrides die with the
        // request scope at the end of HandleAsync.
        var tenantContext = requestServices.GetRequiredService<ITenantContext>();
        var userContext = requestServices.GetRequiredService<ICurrentUserContext>();
        tenantContext.TenantId = payload.TenantId;
        userContext.UserId = payload.UserId;

        var slice = ResolveSlice(payload.SubAgentName, requestServices).ToList();
        var matched = slice.OfType<AIFunction>().FirstOrDefault(t => string.Equals(t.Name, req.Name, StringComparison.Ordinal));
        if (matched is null)
        {
            await Send.ResponseAsync(
                new CodeActCallbackResponse(
                    Status: "error",
                    Result: null,
                    Error: new CodeActCallbackError(
                        Code: "tool_not_found",
                        Message: $"Tool '{req.Name}' is allowed by the nonce but not present in the {payload.SubAgentName} slice. Likely a code/whitelist mismatch.")),
                statusCode: 404,
                cancellation: ct);
            return;
        }

        try
        {
            var args = ParseArgs(req.Args);
            var result = await matched.InvokeAsync(new AIFunctionArguments(args), ct).ConfigureAwait(false);
            // Forward a trace header so the sandbox-side can log remaining
            // budget without an extra round-trip.
            HttpContext.Response.Headers["x-aonik-codeact-budget-remaining"]
                = _nonceService.PeekBudget(payload.Jti).ToString(System.Globalization.CultureInfo.InvariantCulture);

            await Send.OkAsync(
                new CodeActCallbackResponse(
                    Status: "ok",
                    Result: result is null ? null : JsonSerializer.SerializeToElement(result),
                    Error: null),
                ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "CodeAct callback failed invoking {ToolName} for sub-agent {SubAgent} (run {RunId})",
                req.Name, payload.SubAgentName, payload.RunId);
            await Send.ResponseAsync(
                new CodeActCallbackResponse(
                    Status: "error",
                    Result: null,
                    Error: new CodeActCallbackError(
                        Code: "tool_invoke_failed",
                        Message: ex.Message)),
                statusCode: 500,
                cancellation: ct);
        }
    }

    private static IEnumerable<AITool> ResolveSlice(string subAgentName, IServiceProvider sp) => subAgentName switch
    {
        "pf-insights" => PersonalFinanceTools.CreateForInsightsSubAgent(sp),
        "pf-forecast" => PersonalFinanceTools.CreateForForecastSubAgent(sp),
        "pf-classify" => PersonalFinanceTools.CreateForClassifySubAgent(sp),
        _ => throw new InvalidOperationException(
            $"ResolveSlice called with invalid sub-agent name '{subAgentName}' — should have been caught earlier."),
    };

    private static Dictionary<string, object?> ParseArgs(JsonElement? args)
    {
        var bag = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (args is null || args.Value.ValueKind != JsonValueKind.Object)
        {
            return bag;
        }
        foreach (var property in args.Value.EnumerateObject())
        {
            bag[property.Name] = property.Value.Clone();
        }
        return bag;
    }
}

public sealed record CodeActCallbackRequest
{
    /// <summary>Route-bound. Opaque HMAC-signed token.</summary>
    public string Nonce { get; init; } = string.Empty;

    /// <summary>Host tool name to invoke (e.g. <c>"pf_get_merchant_history"</c>).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Tool arguments as a JSON object. Keys map to parameter names.</summary>
    public JsonElement? Args { get; init; }
}

public sealed record CodeActCallbackResponse(
    string Status,
    JsonElement? Result,
    CodeActCallbackError? Error);

public sealed record CodeActCallbackError(string Code, string Message);

/// <summary>
/// Structural validation only — semantic checks (nonce signature, tool whitelist,
/// budget, sub-agent name) live inside <see cref="CodeActCallbackEndpoint"/>
/// because they need access to the nonce service + request scope.
/// </summary>
public sealed class CodeActCallbackRequestValidator : Validator<CodeActCallbackRequest>
{
    public CodeActCallbackRequestValidator()
    {
        RuleFor(x => x.Nonce).NotEmpty().WithMessage("Nonce is required (passed in the route).");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Tool name is required.");
        RuleFor(x => x.Name).MaximumLength(256).WithMessage("Tool name is unreasonably long.");
    }
}
