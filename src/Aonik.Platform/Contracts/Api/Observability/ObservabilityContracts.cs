using System.Text.Json;

namespace Aonik.Platform.Contracts.Api.Observability;

// ── Request ──────────────────────────────────────────────────────────

public record ObservabilityQueryRequest
{
    [FastEndpoints.QueryParam]
    public string TimeRange { get; init; } = "24h";

    [FastEndpoints.QueryParam]
    public string? OperationId { get; init; }

    /// <summary>
    /// Optional severity filter for log queries. One of "debug", "info",
    /// "warn", "error", or "all" (default). Pushed down into the KQL so
    /// the take-N window picks recent rows of the requested severity
    /// rather than client-side filtering an info-heavy slice.
    /// </summary>
    [FastEndpoints.QueryParam]
    public string? Severity { get; init; }
}

// ── Time Series ──────────────────────────────────────────────────────

public record TimeSeriesPoint(DateTime Timestamp, double Value);

// ── Overview ─────────────────────────────────────────────────────────

public record RequestMetrics(
    long Total,
    double RatePerMinute,
    IReadOnlyList<TimeSeriesPoint> TimeSeries);

public record ErrorMetrics(
    long Total,
    double ErrorRatePercent,
    IReadOnlyList<TimeSeriesPoint> TimeSeries,
    IReadOnlyList<ErrorGroup> TopErrors);

public record LatencyMetrics(
    double P50Ms,
    double P95Ms,
    double P99Ms,
    IReadOnlyList<TimeSeriesPoint> TimeSeries);

public record ObservabilityOverviewResponse(
    bool Configured,
    RequestMetrics? Requests,
    ErrorMetrics? Errors,
    LatencyMetrics? Latency);

// ── Errors ───────────────────────────────────────────────────────────

/// <summary>
/// A group of exceptions sharing the same App Insights <c>problemId</c>
/// fingerprint. The identity fields (type, outerMessage, innermostMessage,
/// method) are enough to recognise the error at a glance; the contextual
/// fields (sampleOperationId, operations, roles) drive drill-down into
/// a specific incident via <c>GET /admin/observability/errors/{problemId}</c>.
/// </summary>
public record ErrorGroup(
    string Type,
    string OuterMessage,
    string InnermostMessage,
    long Count,
    DateTime LastSeen,
    string? ProblemId = null,
    string? Method = null,
    string? SampleOperationId = null,
    IReadOnlyList<string>? Operations = null,
    IReadOnlyList<string>? Roles = null);

public record ErrorsResponse(
    bool Configured,
    IReadOnlyList<ErrorGroup> Errors);

/// <summary>
/// A single frame of a parsed exception stack as returned by App Insights.
/// <see cref="Level"/> is the depth from the innermost throw (0 = deepest).
/// </summary>
public record ErrorStackFrame(
    int Level,
    string? Method,
    string? Assembly,
    string? FileName,
    int? Line);

/// <summary>
/// Drill-down payload for a single error group — pulls one representative
/// exception from the <c>exceptions</c> table so the UI can show the full
/// parsed stack and Serilog scope properties (tenant, user, thread, etc.)
/// without bloating the errors list.
/// </summary>
public record ErrorDetailResponse(
    bool Configured,
    bool Found,
    string? ProblemId,
    string? Type,
    string? OuterType,
    string? OuterMessage,
    string? InnermostMessage,
    string? Method,
    string? OperationName,
    string? OperationId,
    string? CloudRoleName,
    string? SeverityLevel,
    DateTime? Timestamp,
    IReadOnlyList<ErrorStackFrame> ParsedStack,
    IReadOnlyDictionary<string, string> CustomDimensions);

// ── Dependencies ─────────────────────────────────────────────────────

public record DependencyHealth(
    string Name,
    string Type,
    long TotalCalls,
    long FailedCalls,
    double SuccessRatePercent,
    double AvgDurationMs);

public record DependencyMetricsResponse(
    bool Configured,
    IReadOnlyList<DependencyHealth> Dependencies);

// ── AI ───────────────────────────────────────────────────────────────

public record AiAgentMetric(
    string AgentName,
    long Calls,
    double AvgDurationMs,
    long TotalTokens);

public record AiMetricsResponse(
    bool Configured,
    long TotalCalls,
    double AvgDurationMs,
    IReadOnlyList<TimeSeriesPoint> TimeSeries,
    IReadOnlyList<AiAgentMetric> ByAgent);

// ── Jobs ─────────────────────────────────────────────────────────────

public record JobExecutionMetric(
    string JobName,
    long Total,
    long Successes,
    long Failures,
    double AvgDurationMs);

public record JobMetricsResponse(
    bool Configured,
    IReadOnlyList<JobExecutionMetric> Jobs);

// ── Structured Logs ──────────────────────────────────────────────────

public record StructuredLogSeverityCounts(
    long Debug,
    long Info,
    long Warn,
    long Error);

public record StructuredLogVolumePoint(
    DateTime Timestamp,
    long Events,
    long Errors);

public record StructuredLogEntry(
    DateTime Timestamp,
    string Severity,
    string Service,
    string Agent,
    string TraceId,
    string Message,
    IReadOnlyDictionary<string, string> Fields);

public record StructuredLogsResponse(
    bool Configured,
    long TotalEvents,
    StructuredLogSeverityCounts Counts,
    IReadOnlyList<StructuredLogVolumePoint> Volume,
    IReadOnlyList<StructuredLogEntry> Entries);

// ── AI Performance ──────────────────────────────────────────────────

public record AiLatencyDistribution(
    double P50Ms, double P75Ms, double P90Ms, double P95Ms, double P99Ms);

public record AiTtftDistribution(
    double P50Ms, double P75Ms, double P90Ms, double P95Ms, double P99Ms);

public record AiTokenUsage(
    long TotalInputTokens, long TotalOutputTokens, long TotalTokens,
    double AvgInputTokensPerRun, double AvgOutputTokensPerRun);

public record AiAgentPerformance(
    string AgentName, long Runs,
    double AvgLatencyMs, double P95LatencyMs,
    double AvgTtftMs, double P95TtftMs,
    long TotalInputTokens, long TotalOutputTokens);

/// <summary>
/// Per-use-case slice of AiCallCompleted telemetry — covers every LLM call
/// (chat, summariser, projector, tools), not just AG-UI agent runs. The
/// "use case" string is supplied by callers via
/// <c>ChatOptions.AdditionalProperties["aonik.use_case"]</c> (defaults to
/// "chat" when unset).
/// </summary>
public record AiUseCasePerformance(
    string UseCase, long Calls,
    double AvgLatencyMs, double P95LatencyMs,
    double AvgTtftMs, double P95TtftMs,
    long TotalInputTokens, long TotalOutputTokens,
    double EstimatedCostUsd);

/// <summary>
/// Per-model slice of AiCallCompleted telemetry. The "model" string is the
/// actual model returned by the provider (preferred) and falls back to the
/// requested model when the provider does not echo one back.
/// </summary>
public record AiModelPerformance(
    string Model, long Calls,
    double AvgLatencyMs, double P95LatencyMs,
    long TotalInputTokens, long TotalOutputTokens,
    double EstimatedCostUsd);

public record AiClientServerComparison(
    double AvgClientRoundTripMs, double AvgServerLatencyMs,
    double AvgNetworkOverheadMs,
    double AvgClientTtftMs, double AvgServerTtftMs);

public record AiStreamingPhaseMetric(
    string PhaseName,
    double P50Ms,
    double P95Ms,
    double P99Ms,
    long Samples);

public record AiStreamingCacheMetric(
    string CacheName,
    long Hits,
    long Misses,
    double HitRatePercent);

public record AiStreamingModeMetric(
    string Mode,
    long Runs,
    double AvgRequestToFirstTokenMs,
    double P95RequestToFirstTokenMs);

public record AiStreamingPhaseTimeSeries(
    string PhaseName,
    IReadOnlyList<TimeSeriesPoint> Points);

public record PersonalFinanceStreamingDiagnostics(
    string AgentName,
    IReadOnlyList<AiStreamingPhaseMetric> Phases,
    IReadOnlyList<AiStreamingCacheMetric> Caches,
    IReadOnlyList<AiStreamingModeMetric> ThreadModes,
    IReadOnlyList<AiStreamingModeMetric> HistorySources,
    IReadOnlyList<AiStreamingPhaseTimeSeries> PhaseTimeSeries);

public record AiPerformanceResponse(
    bool Configured,
    AiLatencyDistribution? Latency,
    AiTtftDistribution? Ttft,
    AiTokenUsage? TokenUsage,
    IReadOnlyList<AiAgentPerformance> ByAgent,
    AiClientServerComparison? ClientServerComparison,
    IReadOnlyList<TimeSeriesPoint> LatencyTimeSeries,
    IReadOnlyList<TimeSeriesPoint> TtftTimeSeries,
    IReadOnlyList<TimeSeriesPoint> TokenTimeSeries,
    IReadOnlyList<AiUseCasePerformance> ByUseCase,
    IReadOnlyList<AiModelPerformance> ByModel,
    PersonalFinanceStreamingDiagnostics? PersonalFinanceStreaming);

/// <summary>
/// Latency distribution for a single retrieval-tier instrument
/// (Qdrant upsert/search, embedding API call, etc.).
/// </summary>
public record RetrievalLatency(
    string Instrument,
    long Samples,
    double AvgMs,
    double P50Ms, double P95Ms, double P99Ms);

/// <summary>
/// Per-collection slice of Qdrant search activity — read from the
/// qdrant.search span attributes on the <c>dependencies</c>/<c>traces</c>
/// tables. Covers hit rate and empty-result visibility.
/// </summary>
public record RetrievalCollectionStats(
    string Collection,
    long Searches,
    double AvgResultCount,
    long EmptySearches,
    double AvgLatencyMs, double P95LatencyMs);

public record RetrievalResponse(
    bool Configured,
    IReadOnlyList<RetrievalLatency> Latencies,
    IReadOnlyList<RetrievalCollectionStats> Collections,
    long EmbeddingErrorCount,
    long TotalSearches,
    long TotalUpserts,
    long TotalEmbeddingCalls,
    IReadOnlyList<TimeSeriesPoint> SearchLatencyTimeSeries,
    IReadOnlyList<TimeSeriesPoint> EmbeddingLatencyTimeSeries);

/// <summary>
/// Per-node payload for the service topology graph. Health is a rollup
/// derived from the last-window error rate and p95 latency.
/// </summary>
public record TopologyNode(
    string Id,
    string Label,
    string Kind,           // "service" | "external" | "datastore"
    string Status,         // "healthy" | "degraded" | "critical" | "unknown"
    long Calls,
    double ErrorRatePct,
    double P95LatencyMs,
    DateTime? LastSeen);

public record TopologyEdge(
    string Source,
    string Target,
    string Kind,           // "http" | "sql" | "grpc" | "queue" | "event"
    long Calls,
    double ErrorRatePct,
    double P95LatencyMs);

public record TopologyResponse(
    bool Configured,
    IReadOnlyList<TopologyNode> Nodes,
    IReadOnlyList<TopologyEdge> Edges,
    DateTime GeneratedAt);

// ── Explain ──────────────────────────────────────────────────────────

/// <summary>
/// LLM-assisted panel summary request. <see cref="Metrics"/> is an opaque
/// JSON blob: the caller picks a small, panel-specific slice of the data
/// currently on screen (totals, percentiles, top agent, etc.) rather than
/// dumping the full response.
/// </summary>
public record ExplainObservabilityPanelRequest(
    string PanelKind,
    JsonElement Metrics);

public record ExplainObservabilityPanelResponse(
    string Summary);

/// <summary>
/// "Interpret this trace with AI" request. <see cref="Spans"/> is the
/// trace-detail observation array as returned by the trace listing
/// endpoint — the server trims it to the most informative rows before
/// passing it to the model. <see cref="TraceId"/> is included only for
/// telemetry correlation; the model does not receive raw IDs in the
/// prompt.
/// </summary>
public record ExplainTraceRequest(
    string TraceId,
    JsonElement Spans);

public record ExplainTraceResponse(
    string Analysis);
