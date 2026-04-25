namespace Aonik.Ai.Contracts.Models;

public sealed record AiTraceListItemResponse
{
    public required Guid RunId { get; init; }
    public DateTime StartedAt { get; init; }
    public required string UseCase { get; init; }
    public required string Outcome { get; init; }
    public string? RequestedModel { get; init; }
    public string? ActualModel { get; init; }
    public int? LatencyMs { get; init; }
    public int? TtftMs { get; init; }
    public int? InputTokens { get; init; }
    public int? OutputTokens { get; init; }
    public int? TotalTokens { get; init; }
    public decimal? EstimatedCostUsd { get; init; }
    public required string TraceStatus { get; init; }
}

public sealed record ListAiTracesResponse(
    IReadOnlyList<AiTraceListItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AiTraceRunRecordResponse
{
    public required Guid RunId { get; init; }
    public DateTime StartedAt { get; init; }
    public required string UseCase { get; init; }
    public Guid AiModelId { get; init; }
    public string? AiModelName { get; init; }
    public Guid? PromptSpecId { get; init; }
    public Guid? AiPolicyId { get; init; }
    public required string InputRefsJson { get; init; }
    public string? OutputRef { get; init; }
    public int TokensUsed { get; init; }
    public decimal CostEstimate { get; init; }
    public int LatencyMs { get; init; }
    public required string Outcome { get; init; }
}

public sealed record AiTraceMetricsResponse
{
    public string? RequestedModel { get; init; }
    public string? ActualModel { get; init; }
    public int? LatencyMs { get; init; }
    public int? TtftMs { get; init; }
    public int? InputTokens { get; init; }
    public int? OutputTokens { get; init; }
    public int? TotalTokens { get; init; }
    public decimal? EstimatedCostUsd { get; init; }
    public DateTime? CompletedAt { get; init; }
}

public sealed record AiTraceTimelineEventResponse
{
    public required DateTime Timestamp { get; init; }
    public required string EventType { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? Status { get; init; }
}

public sealed record AiTraceRawTelemetryEventResponse
{
    public required DateTime Timestamp { get; init; }
    public required string Message { get; init; }
    public required Dictionary<string, string?> Dimensions { get; init; }
}

public sealed record AiTraceRunDetailResponse
{
    public required AiTraceRunRecordResponse Run { get; init; }
    public AiTraceMetricsResponse? Metrics { get; init; }
    public required IReadOnlyList<AiTraceTimelineEventResponse> Timeline { get; init; }
    public required IReadOnlyList<AiTraceRawTelemetryEventResponse> RawTelemetry { get; init; }
    public required string TraceStatus { get; init; }
}

public sealed record ListAiTracesRequest
{
    [FastEndpoints.QueryParam]
    public int? Page { get; init; }

    [FastEndpoints.QueryParam]
    public int? PageSize { get; init; }

    [FastEndpoints.QueryParam]
    public string? UseCase { get; init; }

    [FastEndpoints.QueryParam]
    public string? Outcome { get; init; }

    [FastEndpoints.QueryParam]
    public string? TimeRange { get; init; }

    [FastEndpoints.QueryParam]
    public Guid? RunId { get; init; }
}

public sealed record GetAiTraceRequest
{
    public Guid RunId { get; init; }
}
