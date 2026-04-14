using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aonik.Ai.Observability;

/// <summary>
/// Outermost <see cref="DelegatingChatClient"/> in the AI pipeline. Emits one
/// structured log (<c>AiCallCompleted</c>) per LLM call regardless of the
/// caller — chat endpoint, background summariser, projector, or agent tool.
///
/// This is the single source of truth the observability dashboard's AI tab
/// queries against (<c>AppInsightsQueryService.GetAiPerformanceAsync</c>).
/// Without it, background AI work bypasses telemetry entirely — which is how
/// the runaway OpenAI spend went unnoticed.
///
/// Callers can attach context to a call by setting:
///   <c>options.AdditionalProperties["aonik.use_case"]</c>   (string)
///   <c>options.AdditionalProperties["aonik.ai_run_id"]</c>  (Guid)
/// Both are optional — the decorator falls back to safe defaults.
/// </summary>
internal sealed class TelemetryChatClient : DelegatingChatClient
{
    public const string UseCasePropertyKey = "aonik.use_case";
    public const string AiRunIdPropertyKey = "aonik.ai_run_id";

    public const string MeterName = "Aonik.Ai.Calls";
    public const string MeterVersion = "1.0.0";

    private static readonly Meter Meter = new(MeterName, MeterVersion);

    private static readonly Counter<long> CallCount = Meter.CreateCounter<long>(
        "aonik.ai.calls",
        description: "Total LLM calls observed by the TelemetryChatClient.");

    private static readonly Histogram<long> LatencyMs = Meter.CreateHistogram<long>(
        "aonik.ai.call.latency_ms",
        description: "End-to-end latency of an LLM call in milliseconds.");

    private static readonly Histogram<long> TtftMs = Meter.CreateHistogram<long>(
        "aonik.ai.call.ttft_ms",
        description: "Time to first streamed token in milliseconds.");

    private static readonly Histogram<long> InputTokens = Meter.CreateHistogram<long>(
        "aonik.ai.call.input_tokens",
        description: "Input tokens consumed by an LLM call.");

    private static readonly Histogram<long> OutputTokens = Meter.CreateHistogram<long>(
        "aonik.ai.call.output_tokens",
        description: "Output tokens produced by an LLM call.");

    private static readonly Histogram<double> EstimatedCostUsd = Meter.CreateHistogram<double>(
        "aonik.ai.call.estimated_cost_usd",
        description: "Estimated USD cost of an LLM call from the static price catalog.");

    private readonly ITenantContext? _tenantContext;
    private readonly ICurrentUserProvider? _currentUserProvider;
    private readonly ILogger<TelemetryChatClient> _logger;

    public TelemetryChatClient(
        IChatClient innerClient,
        ILogger<TelemetryChatClient> logger,
        ITenantContext? tenantContext = null,
        ICurrentUserProvider? currentUserProvider = null)
        : base(innerClient)
    {
        _logger = logger;
        _tenantContext = tenantContext;
        _currentUserProvider = currentUserProvider;
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var (useCase, aiRunId) = ExtractCallContext(options);
        var requestedModel = options?.ModelId;

        ChatResponse response;
        try
        {
            response = await base.GetResponseAsync(messages, options, cancellationToken);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            EmitTelemetry(
                useCase: useCase,
                aiRunId: aiRunId,
                operation: "chat",
                requestedModel: requestedModel,
                actualModel: null,
                latencyMs: stopwatch.ElapsedMilliseconds,
                ttftMs: null,
                inputTokens: 0,
                outputTokens: 0,
                outcome: "error",
                error: ex);
            throw;
        }

        stopwatch.Stop();

        var usage = response.Usage;
        EmitTelemetry(
            useCase: useCase,
            aiRunId: aiRunId,
            operation: "chat",
            requestedModel: requestedModel,
            actualModel: response.ModelId,
            latencyMs: stopwatch.ElapsedMilliseconds,
            ttftMs: null,
            inputTokens: (int)(usage?.InputTokenCount ?? 0),
            outputTokens: (int)(usage?.OutputTokenCount ?? 0),
            outcome: "success",
            error: null);

        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var (useCase, aiRunId) = ExtractCallContext(options);
        var requestedModel = options?.ModelId;

        long? ttftMs = null;
        long inputTokens = 0;
        long outputTokens = 0;
        string? actualModel = null;
        var outcome = "success";
        Exception? failure = null;

        // Manual enumerator so we can wrap MoveNextAsync in try/catch — `yield`
        // can't live inside a try with a catch clause.
        IAsyncEnumerator<ChatResponseUpdate>? enumerator = null;
        try
        {
            enumerator = base.GetStreamingResponseAsync(messages, options, cancellationToken)
                .GetAsyncEnumerator(cancellationToken);

            while (true)
            {
                bool hasNext;
                ChatResponseUpdate? current = null;
                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                    if (hasNext)
                    {
                        current = enumerator.Current;
                    }
                }
                catch (Exception ex)
                {
                    outcome = "error";
                    failure = ex;
                    throw;
                }

                if (!hasNext)
                {
                    break;
                }

                if (ttftMs is null && current!.Contents.OfType<TextContent>().Any(t => !string.IsNullOrEmpty(t.Text)))
                {
                    ttftMs = stopwatch.ElapsedMilliseconds;
                }

                actualModel ??= current!.ModelId;

                foreach (var usage in current!.Contents.OfType<UsageContent>())
                {
                    if (usage.Details.InputTokenCount is { } inTokens) inputTokens = inTokens;
                    if (usage.Details.OutputTokenCount is { } outTokens) outputTokens = outTokens;
                }

                yield return current!;
            }
        }
        finally
        {
            if (enumerator is not null)
            {
                await enumerator.DisposeAsync();
            }

            stopwatch.Stop();
            EmitTelemetry(
                useCase: useCase,
                aiRunId: aiRunId,
                operation: "chat.stream",
                requestedModel: requestedModel,
                actualModel: actualModel,
                latencyMs: stopwatch.ElapsedMilliseconds,
                ttftMs: ttftMs,
                inputTokens: (int)inputTokens,
                outputTokens: (int)outputTokens,
                outcome: outcome,
                error: failure);
        }
    }

    private static (string UseCase, Guid? AiRunId) ExtractCallContext(ChatOptions? options)
    {
        var useCase = "chat";
        Guid? aiRunId = null;

        if (options?.AdditionalProperties is { } props)
        {
            if (props.TryGetValue(UseCasePropertyKey, out var useCaseValue) && useCaseValue is string s && !string.IsNullOrWhiteSpace(s))
            {
                useCase = s;
            }

            if (props.TryGetValue(AiRunIdPropertyKey, out var runIdValue))
            {
                aiRunId = runIdValue switch
                {
                    Guid g => g,
                    string str when Guid.TryParse(str, out var parsed) => parsed,
                    _ => null
                };
            }
        }

        return (useCase, aiRunId);
    }

    private void EmitTelemetry(
        string useCase,
        Guid? aiRunId,
        string operation,
        string? requestedModel,
        string? actualModel,
        long latencyMs,
        long? ttftMs,
        int inputTokens,
        int outputTokens,
        string outcome,
        Exception? error)
    {
        var totalTokens = inputTokens + outputTokens;
        var modelForCost = actualModel ?? requestedModel;
        var estimatedCost = AiCostCatalog.Estimate(modelForCost, inputTokens, outputTokens);
        var tenantId = SafeTenantId();
        var userId = SafeUserId();

        // Structured log — single message name `AiCallCompleted` so KQL can
        // filter with `message startswith "AiCallCompleted"` exactly like the
        // legacy `AguiRunCompleted` line.
        if (error is null)
        {
            _logger.LogInformation(
                "AiCallCompleted: UseCase={UseCase} Operation={Operation} RequestedModel={RequestedModel} ActualModel={ActualModel} LatencyMs={LatencyMs} TtftMs={TtftMs} InputTokens={InputTokens} OutputTokens={OutputTokens} TotalTokens={TotalTokens} EstimatedCostUsd={EstimatedCostUsd} Outcome={Outcome} TenantId={TenantId} UserId={UserId} AiRunId={AiRunId}",
                useCase,
                operation,
                requestedModel,
                actualModel,
                latencyMs,
                ttftMs,
                inputTokens,
                outputTokens,
                totalTokens,
                estimatedCost,
                outcome,
                tenantId,
                userId,
                aiRunId);
        }
        else
        {
            _logger.LogWarning(
                error,
                "AiCallCompleted: UseCase={UseCase} Operation={Operation} RequestedModel={RequestedModel} ActualModel={ActualModel} LatencyMs={LatencyMs} TtftMs={TtftMs} InputTokens={InputTokens} OutputTokens={OutputTokens} TotalTokens={TotalTokens} EstimatedCostUsd={EstimatedCostUsd} Outcome={Outcome} TenantId={TenantId} UserId={UserId} AiRunId={AiRunId} ErrorType={ErrorType}",
                useCase,
                operation,
                requestedModel,
                actualModel,
                latencyMs,
                ttftMs,
                inputTokens,
                outputTokens,
                totalTokens,
                estimatedCost,
                outcome,
                tenantId,
                userId,
                aiRunId,
                error.GetType().Name);
        }

        // Metrics — tagged for slicing in dashboards.
        var tags = new TagList
        {
            { "use_case", useCase },
            { "operation", operation },
            { "model", modelForCost ?? "unknown" },
            { "outcome", outcome },
        };

        CallCount.Add(1, tags);
        LatencyMs.Record(latencyMs, tags);
        if (ttftMs is { } ttft) TtftMs.Record(ttft, tags);
        if (inputTokens > 0) InputTokens.Record(inputTokens, tags);
        if (outputTokens > 0) OutputTokens.Record(outputTokens, tags);
        if (estimatedCost > 0) EstimatedCostUsd.Record(estimatedCost, tags);
    }

    private Guid? SafeTenantId()
    {
        try { return _tenantContext?.TenantId; }
        catch { return null; }
    }

    private Guid? SafeUserId()
    {
        try
        {
            return _currentUserProvider is not null && _currentUserProvider.TryGetCurrentUserId(out var id)
                ? id
                : null;
        }
        catch
        {
            return null;
        }
    }
}
