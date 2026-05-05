using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aonik.Ai.Contracts.Models;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Ai.Services;

internal sealed class AiTraceQueryService
{
    private const string TraceStatusDbOnly = "DbOnly";
    private const string TraceStatusDbAndTelemetry = "DbAndTelemetry";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly AiDbContext _dbContext;
    private readonly ISettingProvider _settingProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFusionCache _cache;
    private readonly ILogger<AiTraceQueryService> _logger;

    public AiTraceQueryService(
        AiDbContext dbContext,
        ISettingProvider settingProvider,
        IHttpClientFactory httpClientFactory,
        IFusionCache cache,
        ILogger<AiTraceQueryService> logger)
    {
        _dbContext = dbContext;
        _settingProvider = settingProvider;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ListAiTracesResponse> ListAsync(ListAiTracesRequest request, CancellationToken cancellationToken = default)
    {
        var page = request.Page.GetValueOrDefault(1);
        var pageSize = request.PageSize.GetValueOrDefault(20);

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        var query = _dbContext.AiRuns.AsQueryable();

        if (request.RunId is { } runId)
        {
            query = query.Where(r => r.Id == runId);
        }

        if (!string.IsNullOrWhiteSpace(request.UseCase))
        {
            var useCase = request.UseCase.Trim();
            query = query.Where(r => r.UseCase == useCase);
        }

        if (!string.IsNullOrWhiteSpace(request.Outcome))
        {
            var outcome = request.Outcome.Trim();
            query = query.Where(r => r.Outcome == outcome);
        }

        if (TryResolveRangeFloor(request.TimeRange, out var rangeFloor))
        {
            query = query.Where(r => r.CreatedAt >= rangeFloor);
        }

        query = query.OrderByDescending(r => r.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var runs = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var modelIds = runs.Select(r => r.AiModelId).Distinct().ToList();
        var models = await _dbContext.AiModels
            .Where(m => modelIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.ModelName, cancellationToken);

        var telemetryByRunId = await GetTraceTelemetryBatchAsync(runs.Select(r => r.Id).ToList(), cancellationToken);

        var items = runs.Select(run =>
        {
            telemetryByRunId.TryGetValue(run.Id, out var telemetry);
            var metrics = telemetry?.Metrics;

            return new AiTraceListItemResponse
            {
                RunId = run.Id,
                StartedAt = run.CreatedAt,
                UseCase = run.UseCase,
                Outcome = run.Outcome,
                RequestedModel = metrics?.RequestedModel,
                ActualModel = metrics?.ActualModel ?? models.GetValueOrDefault(run.AiModelId),
                LatencyMs = metrics?.LatencyMs ?? NullIfZero(run.LatencyMs),
                TtftMs = metrics?.TtftMs,
                InputTokens = metrics?.InputTokens,
                OutputTokens = metrics?.OutputTokens,
                TotalTokens = metrics?.TotalTokens ?? NullIfZero(run.TokensUsed),
                EstimatedCostUsd = metrics?.EstimatedCostUsd ?? NullIfZero(run.CostEstimate),
                TraceStatus = telemetry is null ? TraceStatusDbOnly : TraceStatusDbAndTelemetry,
            };
        }).ToList();

        return new ListAiTracesResponse(items, totalCount, page, pageSize);
    }

    public async Task<AiTraceRunDetailResponse?> GetAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await _dbContext.AiRuns.FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        var modelName = await _dbContext.AiModels
            .Where(m => m.Id == run.AiModelId)
            .Select(m => m.ModelName)
            .FirstOrDefaultAsync(cancellationToken);

        var telemetry = await BuildDetailTelemetryEnvelopeAsync(runId, cancellationToken);
        var completedAt = telemetry?.Metrics?.CompletedAt
            ?? InferCompletedAt(run.CreatedAt, telemetry?.Metrics?.LatencyMs ?? run.LatencyMs);

        var timeline = BuildTimeline(run, telemetry?.Events ?? [], completedAt);

        return new AiTraceRunDetailResponse
        {
            Run = new AiTraceRunRecordResponse
            {
                RunId = run.Id,
                StartedAt = run.CreatedAt,
                UseCase = run.UseCase,
                AiModelId = run.AiModelId,
                AiModelName = modelName,
                PromptSpecId = run.PromptSpecId,
                AiPolicyId = run.AiPolicyId,
                InputRefsJson = run.InputRefsJson,
                OutputRef = run.OutputRef,
                TokensUsed = run.TokensUsed,
                CostEstimate = run.CostEstimate,
                LatencyMs = run.LatencyMs,
                Outcome = run.Outcome,
            },
            Metrics = telemetry?.Metrics,
            Timeline = timeline,
            RawTelemetry = telemetry?.Events ?? [],
            TraceStatus = telemetry is null ? TraceStatusDbOnly : TraceStatusDbAndTelemetry,
        };
    }

    private async Task<Dictionary<Guid, AiTraceTelemetryEnvelope>> GetTraceTelemetryBatchAsync(
        IReadOnlyCollection<Guid> runIds,
        CancellationToken cancellationToken)
    {
        if (runIds.Count == 0)
        {
            return [];
        }

        var (appId, apiKey) = await GetCredentialsAsync(cancellationToken);
        if (appId is null || apiKey is null)
        {
            return [];
        }

        var distinctRunIds = runIds.Distinct().ToList();
        var quotedRunIds = string.Join(", ", distinctRunIds.Select(id => $"\"{id:D}\""));
        var cacheKey = $"ai-traces:batch:{string.Join('|', distinctRunIds.Order())}";

        var rows = await _cache.GetOrSetAsync(
            cacheKey,
            async ct => await ExecuteQueryAsync(appId, apiKey, BuildBatchKql(quotedRunIds), ct),
            new FusionCacheEntryOptions(TimeSpan.FromSeconds(60)),
            cancellationToken) ?? [];

        var result = new Dictionary<Guid, AiTraceTelemetryEnvelope>();

        foreach (var row in rows)
        {
            var parsed = ParseTraceTelemetryRow(row);
            if (parsed is null)
            {
                continue;
            }

            result[parsed.RunId] = new AiTraceTelemetryEnvelope
            {
                Metrics = new AiTraceMetricsResponse
                {
                    RequestedModel = parsed.RequestedModel,
                    ActualModel = parsed.ActualModel,
                    LatencyMs = parsed.LatencyMs,
                    TtftMs = parsed.TtftMs,
                    InputTokens = parsed.InputTokens,
                    OutputTokens = parsed.OutputTokens,
                    TotalTokens = parsed.TotalTokens,
                    EstimatedCostUsd = parsed.EstimatedCostUsd,
                    CompletedAt = parsed.Timestamp,
                },
                Events =
                [
                    new AiTraceRawTelemetryEventResponse
                    {
                        Timestamp = parsed.Timestamp,
                        Message = parsed.Message,
                        Dimensions = parsed.Dimensions,
                    }
                ]
            };
        }

        return result;
    }

    private async Task<IReadOnlyList<AiTraceRawTelemetryEventResponse>> GetDetailTelemetryEventsAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var (appId, apiKey) = await GetCredentialsAsync(cancellationToken);
        if (appId is null || apiKey is null)
        {
            return [];
        }

        var cacheKey = $"ai-traces:detail:{runId:D}";
        var rows = await _cache.GetOrSetAsync(
            cacheKey,
            async ct => await ExecuteQueryAsync(appId, apiKey, BuildDetailKql(runId), ct),
            new FusionCacheEntryOptions(TimeSpan.FromSeconds(60)),
            cancellationToken) ?? [];

        return rows
            .Select(ParseTraceTelemetryRow)
            .Where(parsed => parsed is not null)
            .Select(parsed => new AiTraceRawTelemetryEventResponse
            {
                Timestamp = parsed!.Timestamp,
                Message = parsed.Message,
                Dimensions = parsed.Dimensions,
            })
            .OrderBy(e => e.Timestamp)
            .ToList();
    }

    private async Task<AiTraceTelemetryEnvelope?> BuildDetailTelemetryEnvelopeAsync(Guid runId, CancellationToken cancellationToken)
    {
        var events = await GetDetailTelemetryEventsAsync(runId, cancellationToken);
        if (events.Count == 0)
        {
            return null;
        }

        var latest = events[^1];

        latest.Dimensions.TryGetValue("RequestedModel", out var requestedModel);
        latest.Dimensions.TryGetValue("ActualModel", out var actualModel);
        latest.Dimensions.TryGetValue("LatencyMs", out var latencyMsRaw);
        latest.Dimensions.TryGetValue("TtftMs", out var ttftMsRaw);
        latest.Dimensions.TryGetValue("InputTokens", out var inputTokensRaw);
        latest.Dimensions.TryGetValue("OutputTokens", out var outputTokensRaw);
        latest.Dimensions.TryGetValue("TotalTokens", out var totalTokensRaw);
        latest.Dimensions.TryGetValue("EstimatedCostUsd", out var costRaw);

        return new AiTraceTelemetryEnvelope
        {
            Metrics = new AiTraceMetricsResponse
            {
                RequestedModel = NullIfWhiteSpace(requestedModel),
                ActualModel = NullIfWhiteSpace(actualModel),
                LatencyMs = ParseNullableInt(latencyMsRaw),
                TtftMs = ParseNullableInt(ttftMsRaw),
                InputTokens = ParseNullableInt(inputTokensRaw),
                OutputTokens = ParseNullableInt(outputTokensRaw),
                TotalTokens = ParseNullableInt(totalTokensRaw),
                EstimatedCostUsd = ParseNullableDecimal(costRaw),
                CompletedAt = latest.Timestamp,
            },
            Events = events,
        };
    }

    private static IReadOnlyList<AiTraceTimelineEventResponse> BuildTimeline(
        Entities.AiRun run,
        IReadOnlyList<AiTraceRawTelemetryEventResponse> rawTelemetry,
        DateTime? completedAt)
    {
        var items = new List<AiTraceTimelineEventResponse>
        {
            new()
            {
                Timestamp = run.CreatedAt,
                EventType = "run.recorded",
                Title = "Run recorded",
                Description = "AiRun created in audit storage.",
                Status = "info",
            }
        };

        items.AddRange(rawTelemetry.Select(evt =>
        {
            evt.Dimensions.TryGetValue("ActualModel", out var actualModel);
            evt.Dimensions.TryGetValue("TotalTokens", out var totalTokens);
            evt.Dimensions.TryGetValue("LatencyMs", out var latencyMs);
            evt.Dimensions.TryGetValue("Outcome", out var outcome);

            var detailParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(actualModel)) detailParts.Add($"model {actualModel}");
            if (!string.IsNullOrWhiteSpace(totalTokens)) detailParts.Add($"{totalTokens} tokens");
            if (!string.IsNullOrWhiteSpace(latencyMs)) detailParts.Add($"{latencyMs}ms latency");

            return new AiTraceTimelineEventResponse
            {
                Timestamp = evt.Timestamp,
                EventType = "ai.call.completed",
                Title = "AI call completed",
                Description = detailParts.Count == 0 ? evt.Message : string.Join(", ", detailParts),
                Status = string.IsNullOrWhiteSpace(outcome) ? "info" : outcome,
            };
        }));

        items.Add(new AiTraceTimelineEventResponse
        {
            Timestamp = completedAt ?? run.CreatedAt,
            EventType = "run.outcome",
            Title = run.Outcome.Equals("Failed", StringComparison.OrdinalIgnoreCase) ? "Run failed" : "Run completed",
            Description = run.OutputRef,
            Status = run.Outcome,
        });

        return items.OrderBy(i => i.Timestamp).ToList();
    }

    private async Task<(string? AppId, string? ApiKey)> GetCredentialsAsync(CancellationToken cancellationToken)
    {
        var appId = await _settingProvider.GetForScopeAsync(
            ObservabilitySettingNames.AppInsightsAppId,
            SettingScope.Global,
            cancellationToken: cancellationToken);
        var apiKey = await _settingProvider.GetForScopeAsync(
            ObservabilitySettingNames.AppInsightsApiKey,
            SettingScope.Global,
            cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogDebug("Application Insights credentials not configured for AI trace enrichment");
            return (null, null);
        }

        return (appId, apiKey);
    }

    private async Task<IReadOnlyList<JsonElement[]>> ExecuteQueryAsync(
        string appId,
        string apiKey,
        string kql,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("AppInsights");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{appId}/query")
        {
            Content = JsonContent.Create(new { query = kql }),
        };
        request.Headers.Add("x-api-key", apiKey);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call Application Insights API for AI traces");
            return [];
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Application Insights AI trace query returned {StatusCode}: {Query}",
                response.StatusCode,
                kql);
            return [];
        }

        var body = await response.Content.ReadFromJsonAsync<AppInsightsQueryResponse>(JsonOptions, cancellationToken);
        if (body?.Tables is not { Count: > 0 })
        {
            return [];
        }

        var table = body.Tables[0];
        return table.Rows.Select(row => row.Select(cell => cell).ToArray()).ToList();
    }

    private static ParsedTraceTelemetryRow? ParseTraceTelemetryRow(JsonElement[] row)
    {
        if (row.Length < 11)
        {
            return null;
        }

        var runIdRaw = GetString(row, 10);
        if (!Guid.TryParse(runIdRaw, out var runId))
        {
            return null;
        }

        return new ParsedTraceTelemetryRow
        {
            RunId = runId,
            Timestamp = ParseDateTime(row, 0),
            Message = GetString(row, 1),
            RequestedModel = NullIfWhiteSpace(GetString(row, 2)),
            ActualModel = NullIfWhiteSpace(GetString(row, 3)),
            LatencyMs = ParseNullableInt(GetString(row, 4)),
            TtftMs = ParseNullableInt(GetString(row, 5)),
            InputTokens = ParseNullableInt(GetString(row, 6)),
            OutputTokens = ParseNullableInt(GetString(row, 7)),
            TotalTokens = ParseNullableInt(GetString(row, 8)),
            EstimatedCostUsd = ParseNullableDecimal(GetString(row, 9)),
            Dimensions = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["RequestedModel"] = NullIfWhiteSpace(GetString(row, 2)),
                ["ActualModel"] = NullIfWhiteSpace(GetString(row, 3)),
                ["LatencyMs"] = NullIfWhiteSpace(GetString(row, 4)),
                ["TtftMs"] = NullIfWhiteSpace(GetString(row, 5)),
                ["InputTokens"] = NullIfWhiteSpace(GetString(row, 6)),
                ["OutputTokens"] = NullIfWhiteSpace(GetString(row, 7)),
                ["TotalTokens"] = NullIfWhiteSpace(GetString(row, 8)),
                ["EstimatedCostUsd"] = NullIfWhiteSpace(GetString(row, 9)),
                ["AiRunId"] = runIdRaw,
                ["Outcome"] = NullIfWhiteSpace(GetString(row, 11)),
                ["UseCase"] = NullIfWhiteSpace(GetString(row, 12)),
            }
        };
    }

    private static string BuildBatchKql(string quotedRunIds) =>
        $"""
        traces
        | where message startswith \"AiCallCompleted\"
        | extend runId = tostring(customDimensions[\"AiRunId\"])
        | where runId in ({quotedRunIds})
        | summarize arg_max(timestamp, message, customDimensions) by runId
        | project timestamp,
            message,
            RequestedModel = tostring(customDimensions[\"RequestedModel\"]),
            ActualModel = tostring(customDimensions[\"ActualModel\"]),
            LatencyMs = tostring(customDimensions[\"LatencyMs\"]),
            TtftMs = tostring(customDimensions[\"TtftMs\"]),
            InputTokens = tostring(customDimensions[\"InputTokens\"]),
            OutputTokens = tostring(customDimensions[\"OutputTokens\"]),
            TotalTokens = tostring(customDimensions[\"TotalTokens\"]),
            EstimatedCostUsd = tostring(customDimensions[\"EstimatedCostUsd\"]),
            AiRunId = runId,
            Outcome = tostring(customDimensions[\"Outcome\"]),
            UseCase = tostring(customDimensions[\"UseCase\"])
        | order by timestamp desc
        """;

    private static string BuildDetailKql(Guid runId) =>
        $"""
        traces
        | where message startswith \"AiCallCompleted\"
        | extend runId = tostring(customDimensions[\"AiRunId\"])
        | where runId == \"{runId:D}\"
        | project timestamp,
            message,
            RequestedModel = tostring(customDimensions[\"RequestedModel\"]),
            ActualModel = tostring(customDimensions[\"ActualModel\"]),
            LatencyMs = tostring(customDimensions[\"LatencyMs\"]),
            TtftMs = tostring(customDimensions[\"TtftMs\"]),
            InputTokens = tostring(customDimensions[\"InputTokens\"]),
            OutputTokens = tostring(customDimensions[\"OutputTokens\"]),
            TotalTokens = tostring(customDimensions[\"TotalTokens\"]),
            EstimatedCostUsd = tostring(customDimensions[\"EstimatedCostUsd\"]),
            AiRunId = runId,
            Outcome = tostring(customDimensions[\"Outcome\"]),
            UseCase = tostring(customDimensions[\"UseCase\"])
        | order by timestamp asc
        """;

    private static DateTime? InferCompletedAt(DateTime startedAt, int? latencyMs)
    {
        if (latencyMs is null || latencyMs <= 0)
        {
            return null;
        }

        return startedAt.AddMilliseconds(latencyMs.Value);
    }

    private static bool TryResolveRangeFloor(string? timeRange, out DateTime rangeFloor)
    {
        var now = DateTime.UtcNow;
        rangeFloor = timeRange switch
        {
            "1h" => now.AddHours(-1),
            "7d" => now.AddDays(-7),
            "30d" => now.AddDays(-30),
            null or "" or "24h" => now.AddHours(-24),
            _ => DateTime.MinValue,
        };

        return rangeFloor != DateTime.MinValue;
    }

    private static int? NullIfZero(int value) => value <= 0 ? null : value;

    private static decimal? NullIfZero(decimal value) => value == 0m ? null : value;

    private static string GetString(JsonElement[] row, int index)
    {
        if (index >= row.Length)
        {
            return string.Empty;
        }

        return row[index].ValueKind switch
        {
            JsonValueKind.String => row[index].GetString() ?? string.Empty,
            JsonValueKind.Null => string.Empty,
            _ => row[index].ToString(),
        };
    }

    private static DateTime ParseDateTime(JsonElement[] row, int index)
    {
        if (index >= row.Length)
        {
            return DateTime.MinValue;
        }

        return row[index].ValueKind == JsonValueKind.String && DateTime.TryParse(row[index].GetString(), out var dt)
            ? dt
            : DateTime.MinValue;
    }

    private static int? ParseNullableInt(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return double.TryParse(raw, out var parsed) && double.IsFinite(parsed)
            ? (int)Math.Round(parsed)
            : null;
    }

    private static decimal? ParseNullableDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return decimal.TryParse(raw, out var parsed) ? parsed : null;
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed class AppInsightsQueryResponse
    {
        public IReadOnlyList<AppInsightsTable> Tables { get; set; } = [];
    }

    private sealed class AppInsightsTable
    {
        public string Name { get; set; } = string.Empty;
        public IReadOnlyList<AppInsightsColumn> Columns { get; set; } = [];
        public IReadOnlyList<IReadOnlyList<JsonElement>> Rows { get; set; } = [];
    }

    private sealed class AppInsightsColumn
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    private sealed record ParsedTraceTelemetryRow
    {
        public required Guid RunId { get; init; }
        public required DateTime Timestamp { get; init; }
        public required string Message { get; init; }
        public string? RequestedModel { get; init; }
        public string? ActualModel { get; init; }
        public int? LatencyMs { get; init; }
        public int? TtftMs { get; init; }
        public int? InputTokens { get; init; }
        public int? OutputTokens { get; init; }
        public int? TotalTokens { get; init; }
        public decimal? EstimatedCostUsd { get; init; }
        public required Dictionary<string, string?> Dimensions { get; init; }
    }

    private sealed record AiTraceTelemetryEnvelope
    {
        public required AiTraceMetricsResponse Metrics { get; init; }
        public required IReadOnlyList<AiTraceRawTelemetryEventResponse> Events { get; init; }
    }
}
