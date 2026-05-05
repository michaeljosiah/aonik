using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
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
    public const string UseCasePropertyKey = AiTelemetry.UseCaseAttribute;
    public const string AiRunIdPropertyKey = AiTelemetry.AiRunIdAttribute;

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
    private readonly bool _enableSensitiveData;

    public TelemetryChatClient(
        IChatClient innerClient,
        ILogger<TelemetryChatClient> logger,
        ITenantContext? tenantContext = null,
        ICurrentUserProvider? currentUserProvider = null,
        bool enableSensitiveData = false)
        : base(innerClient)
    {
        _logger = logger;
        _tenantContext = tenantContext;
        _currentUserProvider = currentUserProvider;
        _enableSensitiveData = enableSensitiveData;
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var messageList = messages.ToList();
        var stopwatch = Stopwatch.StartNew();
        var initialContext = ExtractCallContext(options);
        var requestedModel = options?.ModelId;

        ChatResponse response;
        try
        {
            response = await base.GetResponseAsync(messageList, options, cancellationToken);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            EmitTelemetry(
                useCase: initialContext.UseCase,
                aiRunId: initialContext.AiRunId,
                operation: "chat",
                requestedModel: requestedModel,
                actualModel: null,
                latencyMs: stopwatch.ElapsedMilliseconds,
                ttftMs: null,
                inputTokens: 0,
                outputTokens: 0,
                outcome: "error",
                error: ex,
                inputJson: SerializeInput(messageList, options),
                outputJson: null);
            throw;
        }

        stopwatch.Stop();

        var usage = response.Usage;
        var finalContext = ExtractCallContext(options);
        EmitTelemetry(
            useCase: finalContext.UseCase,
            aiRunId: finalContext.AiRunId,
            operation: "chat",
            requestedModel: requestedModel,
            actualModel: response.ModelId,
            latencyMs: stopwatch.ElapsedMilliseconds,
            ttftMs: null,
            inputTokens: (int)(usage?.InputTokenCount ?? 0),
            outputTokens: (int)(usage?.OutputTokenCount ?? 0),
            outcome: "success",
            error: null,
            inputJson: SerializeInput(messageList, options),
            outputJson: SerializeOutput(response));

        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messageList = messages.ToList();
        var stopwatch = Stopwatch.StartNew();
        var initialContext = ExtractCallContext(options);
        var requestedModel = options?.ModelId;

        long? ttftMs = null;
        long inputTokens = 0;
        long outputTokens = 0;
        string? actualModel = null;
        var outcome = "success";
        Exception? failure = null;
        var responseText = _enableSensitiveData ? new System.Text.StringBuilder() : null;

        // Manual enumerator so we can wrap MoveNextAsync in try/catch — `yield`
        // can't live inside a try with a catch clause.
        IAsyncEnumerator<ChatResponseUpdate>? enumerator = null;
        try
        {
            enumerator = base.GetStreamingResponseAsync(messageList, options, cancellationToken)
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

                if (responseText is not null)
                {
                    foreach (var textContent in current!.Contents.OfType<TextContent>())
                    {
                        responseText.Append(textContent.Text);
                    }
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
            var finalContext = ExtractCallContext(options);
            EmitTelemetry(
                useCase: finalContext.UseCase,
                aiRunId: finalContext.AiRunId ?? initialContext.AiRunId,
                operation: "chat.stream",
                requestedModel: requestedModel,
                actualModel: actualModel,
                latencyMs: stopwatch.ElapsedMilliseconds,
                ttftMs: ttftMs,
                inputTokens: (int)inputTokens,
                outputTokens: (int)outputTokens,
                outcome: outcome,
                error: failure,
                inputJson: SerializeInput(messageList, options),
                outputJson: SerializeStreamingOutput(responseText?.ToString()));
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
        Exception? error,
        string? inputJson,
        string? outputJson)
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

        // Metrics — tagged for slicing in dashboards. tenant.id is included
        // so per-tenant token / latency / cost panels work without scraping
        // the AnkAiRuns table; it's the empty Guid when no tenant scope is
        // bound (e.g. background jobs running platform-wide).
        var tags = new TagList
        {
            { "tenant.id", tenantId.ToString() },
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

        EmitTraceObservation(
            useCase,
            aiRunId,
            operation,
            modelForCost,
            latencyMs,
            ttftMs,
            inputTokens,
            outputTokens,
            totalTokens,
            estimatedCost,
            outcome,
            tenantId,
            userId,
            inputJson,
            outputJson,
            error);
    }

    private void EmitTraceObservation(
        string useCase,
        Guid? aiRunId,
        string operation,
        string? model,
        long latencyMs,
        long? ttftMs,
        int inputTokens,
        int outputTokens,
        int totalTokens,
        double estimatedCost,
        string outcome,
        Guid? tenantId,
        Guid? userId,
        string? inputJson,
        string? outputJson,
        Exception? error)
    {
        var current = Activity.Current;
        var observationId = current?.SpanId.ToString() ?? Guid.NewGuid().ToString("N");
        var traceId = current?.TraceId.ToString() ?? observationId;
        var parentObservationId = current is null || current.ParentSpanId == default
            ? null
            : current.ParentSpanId.ToString();
        var latencySeconds = latencyMs / 1000.0;
        var ttftSeconds = ttftMs is null ? (double?)null : ttftMs.Value / 1000.0;
        var level = error is null ? "DEFAULT" : "ERROR";
        var metadataJson = JsonSerializer.Serialize(new
        {
            tenantId,
            userId,
            useCase,
            operation,
            outcome,
            errorType = error?.GetType().Name,
        });

        current?.SetTag(AiTelemetry.AiRunIdAttribute, aiRunId?.ToString());
        current?.SetTag(AiTelemetry.ObservationIdAttribute, observationId);
        current?.SetTag(AiTelemetry.ObservationTypeAttribute, "GENERATION");
        current?.SetTag(AiTelemetry.ObservationNameAttribute, operation);
        current?.SetTag(AiTelemetry.ObservationTraceNameAttribute, useCase);
        current?.SetTag("aonik.ai.latency_ms", latencyMs);
        current?.SetTag("aonik.ai.cost_usd", estimatedCost);
        current?.SetTag("aonik.ai.input_tokens", inputTokens);
        current?.SetTag("aonik.ai.output_tokens", outputTokens);
        current?.SetTag("aonik.ai.total_tokens", totalTokens);
        current?.SetTag("aonik.ai.model", model);
        if (_enableSensitiveData)
        {
            current?.SetTag("aonik.ai.input", inputJson);
            current?.SetTag("aonik.ai.output", outputJson);
        }

        _logger.LogInformation(
            "{TraceObservationLogName}: ObservationId={ObservationId} TraceId={TraceId} ParentObservationId={ParentObservationId} AiRunId={AiRunId} ObservationType={ObservationType} Name={Name} TraceName={TraceName} InputJson={InputJson} OutputJson={OutputJson} MetadataJson={MetadataJson} Level={Level} LatencySeconds={LatencySeconds} CostUsd={CostUsd} TimeToFirstTokenSeconds={TimeToFirstTokenSeconds} ProvidedModel={ProvidedModel} InputTokens={InputTokens} OutputTokens={OutputTokens} TotalTokens={TotalTokens}",
            AiTelemetry.TraceObservationLogName,
            observationId,
            traceId,
            parentObservationId,
            aiRunId,
            "GENERATION",
            operation,
            useCase,
            inputJson,
            outputJson,
            metadataJson,
            level,
            latencySeconds,
            estimatedCost,
            ttftSeconds,
            model,
            inputTokens,
            outputTokens,
            totalTokens);
    }

    /// <summary>
    /// Serializer options for the verbose request/response capture.
    /// Indented + ignoring nulls keeps the rendered JSON in the trace
    /// explorer readable, and the per-element preserves are large enough
    /// to cover real prompts without exploding the customDimensions
    /// payload (Application Insights row size ceiling is ~1 MB).
    /// </summary>
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Captures the FULL payload sent to the LLM — every message with
    /// every content block (text, tool calls, tool results, reasoning,
    /// usage), plus the ChatOptions (model id, sampling params, response
    /// format, the bound Tools list with their JSON schemas, additional
    /// properties). Previously we only kept role + concatenated text,
    /// which hid tool definitions, structured-output schemas, and tool
    /// call results — the things you most need when debugging "why did
    /// the model do X". Gated by <see cref="_enableSensitiveData"/>.
    /// </summary>
    private string? SerializeInput(IReadOnlyList<ChatMessage> messages, ChatOptions? options)
    {
        if (!_enableSensitiveData)
        {
            return null;
        }

        var payload = new
        {
            options = options is null ? null : SerializeOptions(options),
            messages = messages.Select(SerializeMessage).ToArray(),
        };

        return JsonSerializer.Serialize(payload, PayloadJsonOptions);
    }

    private static object SerializeMessage(ChatMessage message)
    {
        return new
        {
            role = message.Role.ToString(),
            authorName = message.AuthorName,
            messageId = message.MessageId,
            contents = message.Contents.Select(SerializeContent).ToArray(),
        };
    }

    private static object SerializeContent(AIContent content) => content switch
    {
        TextContent text => new
        {
            kind = "text",
            text = text.Text,
        },
        FunctionCallContent fnCall => new
        {
            kind = "tool_call",
            callId = fnCall.CallId,
            name = fnCall.Name,
            arguments = fnCall.Arguments,
        },
        FunctionResultContent fnResult => new
        {
            kind = "tool_result",
            callId = fnResult.CallId,
            result = SafeStringify(fnResult.Result),
        },
        UsageContent usage => new
        {
            kind = "usage",
            inputTokens = usage.Details.InputTokenCount,
            outputTokens = usage.Details.OutputTokenCount,
            totalTokens = usage.Details.TotalTokenCount,
        },
        TextReasoningContent reasoning => new
        {
            kind = "reasoning",
            text = reasoning.Text,
        },
        _ => new
        {
            kind = content.GetType().Name,
            raw = SafeStringify(content),
        },
    };

    private static object SerializeOptions(ChatOptions options)
    {
        return new
        {
            modelId = options.ModelId,
            temperature = options.Temperature,
            topP = options.TopP,
            topK = options.TopK,
            maxOutputTokens = options.MaxOutputTokens,
            frequencyPenalty = options.FrequencyPenalty,
            presencePenalty = options.PresencePenalty,
            responseFormat = options.ResponseFormat?.GetType().Name,
            stopSequences = options.StopSequences,
            seed = options.Seed,
            toolMode = options.ToolMode?.GetType().Name,
            tools = options.Tools?.Select(SerializeTool).ToArray(),
            additionalProperties = options.AdditionalProperties?.ToDictionary(
                kv => kv.Key,
                kv => SafeStringify(kv.Value)),
        };
    }

    private static object SerializeTool(AITool tool)
    {
        // AITool only exposes Name + Description publicly across all
        // implementations; AIFunction adds JsonSchema. Fall through to
        // type name when richer details aren't reachable without a cast.
        if (tool is AIFunction fn)
        {
            return new
            {
                kind = "function",
                name = fn.Name,
                description = fn.Description,
                parametersSchema = fn.JsonSchema,
            };
        }

        return new
        {
            kind = tool.GetType().Name,
            name = tool.Name,
            description = tool.Description,
        };
    }

    /// <summary>
    /// Captures the full assistant response — every content block in the
    /// final message (text, tool calls the model decided to make,
    /// reasoning), plus usage and finish reason. Replaces the old
    /// text-only serializer.
    /// </summary>
    private string? SerializeOutput(ChatResponse response)
    {
        if (!_enableSensitiveData) return null;

        var lastMessage = response.Messages.LastOrDefault();
        var payload = new
        {
            modelId = response.ModelId,
            finishReason = response.FinishReason?.ToString(),
            usage = response.Usage is null ? null : new
            {
                inputTokens = response.Usage.InputTokenCount,
                outputTokens = response.Usage.OutputTokenCount,
                totalTokens = response.Usage.TotalTokenCount,
            },
            text = response.Text,
            contents = lastMessage?.Contents.Select(SerializeContent).ToArray(),
        };

        return JsonSerializer.Serialize(payload, PayloadJsonOptions);
    }

    /// <summary>
    /// Streaming path: we don't have the structured response object on
    /// hand — only the accumulated text — so we emit the same shape with
    /// only the text populated.
    /// </summary>
    private string? SerializeStreamingOutput(string? text)
    {
        if (!_enableSensitiveData || string.IsNullOrEmpty(text)) return null;
        return JsonSerializer.Serialize(new { text }, PayloadJsonOptions);
    }

    private static string? SafeStringify(object? value)
    {
        if (value is null) return null;
        if (value is string s) return s;
        try { return JsonSerializer.Serialize(value); }
        catch { return value.ToString(); }
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
