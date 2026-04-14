namespace Aonik.Platform.Contracts.Api.Observability;

// ── Request ──────────────────────────────────────────────────────────

public record ObservabilityQueryRequest
{
    [FastEndpoints.QueryParam]
    public string TimeRange { get; init; } = "24h";
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

public record ErrorGroup(
    string Type,
    string OuterMessage,
    string InnermostMessage,
    long Count,
    DateTime LastSeen);

public record ErrorsResponse(
    bool Configured,
    IReadOnlyList<ErrorGroup> Errors);

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
    IReadOnlyList<AiModelPerformance> ByModel);
