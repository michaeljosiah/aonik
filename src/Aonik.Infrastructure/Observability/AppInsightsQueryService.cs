using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aonik.Platform.Contracts.Api.Observability;
using Aonik.Platform.Contracts.Services.Operations;
using Aonik.Platform.Contracts.Services.Observability;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions.Settings;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Infrastructure.Observability;

public class AppInsightsQueryService : IObservabilityService
{
    private readonly ISettingProvider _settingProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFusionCache _cache;
    private readonly IRuntimeOperationsService _runtimeOperationsService;
    private readonly ILogger<AppInsightsQueryService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public AppInsightsQueryService(
        ISettingProvider settingProvider,
        IHttpClientFactory httpClientFactory,
        IFusionCache cache,
        IRuntimeOperationsService runtimeOperationsService,
        ILogger<AppInsightsQueryService> logger)
    {
        _settingProvider = settingProvider;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _runtimeOperationsService = runtimeOperationsService;
        _logger = logger;
    }

    // ── Public API ───────────────────────────────────────────────────

    public async Task<ObservabilityOverviewResponse> GetOverviewAsync(
        string timeRange, CancellationToken cancellationToken = default)
    {
        var (appId, apiKey) = await GetCredentialsAsync(cancellationToken);
        if (appId is null || apiKey is null)
            return new ObservabilityOverviewResponse(false, null, null, null);

        var range = ParseTimeRange(timeRange);

        var requestTimeSeriesTask = CachedQueryAsync(
            $"observability:requestTimeSeries:{timeRange}", appId, apiKey,
            $"requests | where timestamp > {range.Ago} | summarize count() by bin(timestamp, {range.Bin}) | order by timestamp asc",
            cancellationToken);

        var errorTimeSeriesTask = CachedQueryAsync(
            $"observability:errorTimeSeries:{timeRange}", appId, apiKey,
            $"requests | where timestamp > {range.Ago} | summarize total=count(), failed=countif(success == false) by bin(timestamp, {range.Bin}) | extend errorRate = round(100.0 * failed / total, 2) | order by timestamp asc",
            cancellationToken);

        var latencyPercentilesTask = CachedQueryAsync(
            $"observability:latencyPercentiles:{timeRange}", appId, apiKey,
            $"requests | where timestamp > {range.Ago} | summarize p50=percentile(duration, 50), p95=percentile(duration, 95), p99=percentile(duration, 99)",
            cancellationToken);

        var latencyTimeSeriesTask = CachedQueryAsync(
            $"observability:latencyTimeSeries:{timeRange}", appId, apiKey,
            $"requests | where timestamp > {range.Ago} | summarize avg(duration) by bin(timestamp, {range.Bin}) | order by timestamp asc",
            cancellationToken);

        // WhenAll runs the four queries in parallel and observes faults from
        // every task even if one throws first. Once it returns, awaiting each
        // sub-task again is a fast path that just unwraps the result — no
        // sync blocking and no AggregateException wrapping (which the previous
        // .Result reads suffered from).
        await Task.WhenAll(requestTimeSeriesTask, errorTimeSeriesTask, latencyPercentilesTask, latencyTimeSeriesTask);

        var requestRows = await requestTimeSeriesTask;
        var errorRows = await errorTimeSeriesTask;
        var latencyPercentileRows = await latencyPercentilesTask;
        var latencyRows = await latencyTimeSeriesTask;

        // Request metrics
        var requestTimeSeries = requestRows.Select(r => new TimeSeriesPoint(
            ParseDateTime(r, 0), ParseDouble(r, 1))).ToList();
        var totalRequests = (long)requestTimeSeries.Sum(p => p.Value);
        var rangeMinutes = range.TotalMinutes;
        var ratePerMinute = rangeMinutes > 0 ? Math.Round(totalRequests / rangeMinutes, 2) : 0;

        var requests = new RequestMetrics(totalRequests, ratePerMinute, requestTimeSeries);

        // Error metrics
        var errorTimeSeries = errorRows.Select(r => new TimeSeriesPoint(
            ParseDateTime(r, 0), ParseDouble(r, 3))).ToList();
        var totalErrors = (long)errorRows.Sum(r => ParseDouble(r, 2));
        var totalForErrorRate = (long)errorRows.Sum(r => ParseDouble(r, 1));
        var errorRate = totalForErrorRate > 0
            ? Math.Round(100.0 * totalErrors / totalForErrorRate, 2)
            : 0;

        // Top errors (separate query). Projection mirrors GetErrorsAsync so
        // the overview and errors tab read the same shape.
        var topErrorRows = await CachedQueryAsync(
            $"observability:topErrors:{timeRange}", appId, apiKey,
            BuildErrorGroupsKql(range.Ago),
            cancellationToken);

        var topErrors = topErrorRows.Select(ParseErrorGroupRow).ToList();

        var errors = new ErrorMetrics(totalErrors, errorRate, errorTimeSeries, topErrors);

        // Latency metrics
        double p50 = 0, p95 = 0, p99 = 0;
        if (latencyPercentileRows.Count > 0)
        {
            p50 = ParseDouble(latencyPercentileRows[0], 0);
            p95 = ParseDouble(latencyPercentileRows[0], 1);
            p99 = ParseDouble(latencyPercentileRows[0], 2);
        }

        var latencyTimeSeries = latencyRows.Select(r => new TimeSeriesPoint(
            ParseDateTime(r, 0), ParseDouble(r, 1))).ToList();

        var latency = new LatencyMetrics(
            Math.Round(p50, 2), Math.Round(p95, 2), Math.Round(p99, 2), latencyTimeSeries);

        return new ObservabilityOverviewResponse(true, requests, errors, latency);
    }

    public async Task<ErrorsResponse> GetErrorsAsync(
        string timeRange, string? operationId = null, CancellationToken cancellationToken = default)
    {
        var (appId, apiKey) = await GetCredentialsAsync(cancellationToken);
        if (appId is null || apiKey is null)
            return new ErrorsResponse(false, []);

        var range = ParseTimeRange(timeRange);

        var rows = await CachedQueryAsync(
            $"observability:errors:{timeRange}:{operationId}", appId, apiKey,
            BuildErrorGroupsKql(range.Ago, operationId),
            cancellationToken);

        var errors = rows.Select(ParseErrorGroupRow).ToList();

        return new ErrorsResponse(true, errors);
    }

    public async Task<ErrorDetailResponse> GetErrorDetailAsync(
        string problemId, string timeRange, CancellationToken cancellationToken = default)
    {
        var (appId, apiKey) = await GetCredentialsAsync(cancellationToken);
        if (appId is null || apiKey is null)
            return new ErrorDetailResponse(
                Configured: false,
                Found: false,
                ProblemId: problemId,
                Type: null, OuterType: null, OuterMessage: null, InnermostMessage: null,
                Method: null, OperationName: null, OperationId: null,
                CloudRoleName: null, SeverityLevel: null, Timestamp: null,
                ParsedStack: [], CustomDimensions: new Dictionary<string, string>());

        var range = ParseTimeRange(timeRange);

        // `problemId` arrives from a URL segment, so escape any quotes/backslashes
        // before interpolating into the KQL string literal. KQL's only string-
        // escape characters are `\` and `"`.
        var escapedProblemId = (problemId ?? string.Empty)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

        var rows = await CachedQueryAsync(
            $"observability:errorDetail:{timeRange}:{problemId}", appId, apiKey,
            $"""
            exceptions
            | where timestamp > {range.Ago}
            | where problemId == "{escapedProblemId}"
            | top 1 by timestamp desc
            | project
                problemId,
                type,
                outerType,
                outerMessage,
                innermostMessage,
                method,
                operation_Name,
                operation_Id,
                cloud_RoleName,
                severityLevel,
                timestamp,
                stackJson = tostring(details[0].parsedStack),
                customDimensionsJson = tostring(customDimensions)
            """,
            cancellationToken);

        if (rows.Count == 0)
        {
            return new ErrorDetailResponse(
                Configured: true,
                Found: false,
                ProblemId: problemId,
                Type: null, OuterType: null, OuterMessage: null, InnermostMessage: null,
                Method: null, OperationName: null, OperationId: null,
                CloudRoleName: null, SeverityLevel: null, Timestamp: null,
                ParsedStack: [], CustomDimensions: new Dictionary<string, string>());
        }

        var row = rows[0];

        var parsedStack = ParseStackJson(GetString(row, 11));
        var customDimensions = ParseCustomDimensionsJson(GetString(row, 12));

        return new ErrorDetailResponse(
            Configured: true,
            Found: true,
            ProblemId: NullIfEmpty(GetString(row, 0)),
            Type: NullIfEmpty(GetString(row, 1)),
            OuterType: NullIfEmpty(GetString(row, 2)),
            OuterMessage: NullIfEmpty(GetString(row, 3)),
            InnermostMessage: NullIfEmpty(GetString(row, 4)),
            Method: NullIfEmpty(GetString(row, 5)),
            OperationName: NullIfEmpty(GetString(row, 6)),
            OperationId: NullIfEmpty(GetString(row, 7)),
            CloudRoleName: NullIfEmpty(GetString(row, 8)),
            SeverityLevel: NullIfEmpty(GetString(row, 9)),
            Timestamp: ParseDateTimeNullable(row, 10),
            ParsedStack: parsedStack,
            CustomDimensions: customDimensions);
    }

    public async Task<DependencyMetricsResponse> GetDependenciesAsync(
        string timeRange, CancellationToken cancellationToken = default)
    {
        var (appId, apiKey) = await GetCredentialsAsync(cancellationToken);
        if (appId is null || apiKey is null)
            return new DependencyMetricsResponse(false, []);

        var range = ParseTimeRange(timeRange);

        var rows = await CachedQueryAsync(
            $"observability:dependencies:{timeRange}", appId, apiKey,
            $"dependencies | where timestamp > {range.Ago} | summarize totalCalls=count(), failedCalls=countif(success == false), avgDuration=avg(duration) by name, type | extend successRate = round(100.0 * (totalCalls - failedCalls) / totalCalls, 2) | order by totalCalls desc",
            cancellationToken);

        var deps = rows.Select(r => new DependencyHealth(
            GetString(r, 0), GetString(r, 1),
            (long)ParseDouble(r, 2), (long)ParseDouble(r, 3),
            ParseDouble(r, 5), Math.Round(ParseDouble(r, 4), 2))).ToList();

        return new DependencyMetricsResponse(true, deps);
    }

    public async Task<AiMetricsResponse> GetAiMetricsAsync(
        string timeRange, CancellationToken cancellationToken = default)
    {
        var (appId, apiKey) = await GetCredentialsAsync(cancellationToken);
        if (appId is null || apiKey is null)
            return new AiMetricsResponse(false, 0, 0, [], []);

        var range = ParseTimeRange(timeRange);

        // Source: the structured `AguiRunCompleted` log emitted by
        // AguiStreamingEndpoint on every chat run. This gives us a reliable
        // per-run row with the agent name already resolved (or
        // "orchestrator" when the master orchestrator handles the turn),
        // which is exactly what the Agent Fleet panel needs.
        //
        // The previous implementation queried the `dependencies` table
        // filtered to `type == "Azure OpenAI"` — AONIK uses the plain
        // OpenAI SDK, so that filter matched nothing and the panel stayed
        // permanently empty. It also grouped by `gen_ai.operation.name`
        // (an operation type, not an agent name), which would have yielded
        // useless rows even if the filter had matched.
        var timeSeriesTask = CachedQueryAsync(
            $"observability:aiTimeSeries:{timeRange}", appId, apiKey,
            $"traces | where timestamp > {range.Ago} | where message startswith \"AguiRunCompleted\" | summarize count() by bin(timestamp, {range.Bin}) | order by timestamp asc",
            cancellationToken);

        var byAgentTask = CachedQueryAsync(
            $"observability:aiByAgent:{timeRange}", appId, apiKey,
            $"traces | where timestamp > {range.Ago} | where message startswith \"AguiRunCompleted\" | extend agentName = tostring(customDimensions[\"AgentName\"]), latencyMs = todouble(customDimensions[\"LatencyMs\"]), totalTokens = tolong(customDimensions[\"TotalTokens\"]) | summarize calls=count(), avgDuration=avg(latencyMs), totalTokens=sum(totalTokens) by agentName | order by calls desc",
            cancellationToken);

        await Task.WhenAll(timeSeriesTask, byAgentTask);

        var timeSeriesRows = await timeSeriesTask;
        var byAgentRows = await byAgentTask;

        var timeSeries = timeSeriesRows.Select(r => new TimeSeriesPoint(
            ParseDateTime(r, 0), ParseDouble(r, 1))).ToList();

        var totalCalls = (long)timeSeries.Sum(p => p.Value);

        var byAgent = byAgentRows.Select(r => new AiAgentMetric(
            GetString(r, 0),
            (long)ParseDouble(r, 1),
            Math.Round(ParseDouble(r, 2), 2),
            (long)ParseDouble(r, 3))).ToList();

        var avgDuration = byAgent.Count > 0
            ? Math.Round(byAgent.Average(a => a.AvgDurationMs), 2)
            : 0;

        return new AiMetricsResponse(true, totalCalls, avgDuration, timeSeries, byAgent);
    }

    public async Task<JobMetricsResponse> GetJobMetricsAsync(
        string timeRange, CancellationToken cancellationToken = default)
    {
        var (appId, apiKey) = await GetCredentialsAsync(cancellationToken);
        if (appId is null || apiKey is null)
            return new JobMetricsResponse(false, []);

        var range = ParseTimeRange(timeRange);

        var rows = await CachedQueryAsync(
            $"observability:jobs:{timeRange}", appId, apiKey,
            $"customEvents | where timestamp > {range.Ago} | where name startswith \"JobExecution\" | extend jobName = tostring(customDimensions[\"job.name\"]), outcome = tostring(customDimensions[\"job.outcome\"]), durationMs = todouble(customDimensions[\"job.duration_ms\"]) | summarize total=count(), successes=countif(outcome == \"Completed\"), failures=countif(outcome != \"Completed\"), avgDuration=avg(durationMs) by jobName | order by total desc",
            cancellationToken);

        var jobs = rows.Select(r => new JobExecutionMetric(
            GetString(r, 0),
            (long)ParseDouble(r, 1),
            (long)ParseDouble(r, 2),
            (long)ParseDouble(r, 3),
            Math.Round(ParseDouble(r, 4), 2))).ToList();

        return new JobMetricsResponse(true, jobs);
    }

    public async Task<StructuredLogsResponse> GetStructuredLogsAsync(
        string timeRange, string? severity = null, CancellationToken cancellationToken = default)
    {
        var (appId, apiKey) = await GetCredentialsAsync(cancellationToken);
        if (appId is null || apiKey is null)
            return new StructuredLogsResponse(
                false,
                0,
                new StructuredLogSeverityCounts(0, 0, 0, 0),
                [],
                []);

        var range = ParseTimeRange(timeRange);

        // Severity filter for the entries query: pushed into KQL so the
        // take-120 window picks rows of the requested severity instead
        // of returning an info-heavy slice that client-side filtering
        // then narrows to nothing. Counts and volume always span all
        // severities so the pill counts and volume sparkline remain
        // accurate regardless of the active filter.
        var normalisedSeverity = NormaliseSeverityFilter(severity);
        var entriesSeverityFilter = normalisedSeverity switch
        {
            "debug" => "| where toint(severityLevel) == 0",
            "info" => "| where isnull(toint(severityLevel)) or toint(severityLevel) == 1",
            "warn" => "| where toint(severityLevel) == 2",
            "error" => "| where toint(severityLevel) >= 3",
            _ => string.Empty,
        };
        var entriesCacheKey = string.IsNullOrEmpty(normalisedSeverity)
            ? $"observability:logs:entries:{timeRange}"
            : $"observability:logs:entries:{timeRange}:{normalisedSeverity}";

        var countsTask = CachedQueryAsync(
            $"observability:logs:counts:{timeRange}", appId, apiKey,
            $$"""
            traces
            | where timestamp > {{range.Ago}}
            | extend severity = toint(severityLevel)
            | summarize
                debug=countif(severity == 0),
                info=countif(isnull(severity) or severity == 1),
                warn=countif(severity == 2),
                error=countif(severity >= 3)
            """,
            cancellationToken);

        var volumeTask = CachedQueryAsync(
            $"observability:logs:volume:{timeRange}", appId, apiKey,
            $$"""
            traces
            | where timestamp > {{range.Ago}}
            | extend severity = toint(severityLevel)
            | summarize
                events=count(),
                errors=countif(severity >= 3)
              by bin(timestamp, {{range.Bin}})
            | order by timestamp asc
            """,
            cancellationToken);

        var entriesTask = CachedQueryAsync(
            entriesCacheKey, appId, apiKey,
            $$"""
            traces
            | where timestamp > {{range.Ago}}
            {{entriesSeverityFilter}}
            | extend severity = case(
                toint(severityLevel) == 0, "debug",
                isnull(toint(severityLevel)) or toint(severityLevel) == 1, "info",
                toint(severityLevel) == 2, "warn",
                toint(severityLevel) >= 3, "error",
                "info")
            | extend service = iff(isempty(tostring(cloud_RoleName)), "—", tostring(cloud_RoleName))
            | extend traceId = iff(isempty(tostring(operation_Id)), "—", tostring(operation_Id))
            | extend agent = coalesce(
                tostring(customDimensions["gen_ai.agent.name"]),
                tostring(customDimensions["aonik.agent.name"]),
                tostring(customDimensions["AgentName"]),
                "—")
            | extend fields = customDimensions
            | project timestamp, severity, service, agent, traceId, message, fields
            | order by timestamp desc
            | take 120
            """,
            cancellationToken);

        await Task.WhenAll(countsTask, volumeTask, entriesTask);

        var countsRows = await countsTask;
        var volumeRows = await volumeTask;
        var entryRows = await entriesTask;

        var counts = countsRows.Count > 0
            ? new StructuredLogSeverityCounts(
                (long)ParseDouble(countsRows[0], 0),
                (long)ParseDouble(countsRows[0], 1),
                (long)ParseDouble(countsRows[0], 2),
                (long)ParseDouble(countsRows[0], 3))
            : new StructuredLogSeverityCounts(0, 0, 0, 0);

        var volume = volumeRows.Select(r => new StructuredLogVolumePoint(
            ParseDateTime(r, 0),
            (long)ParseDouble(r, 1),
            (long)ParseDouble(r, 2))).ToList();

        var entries = entryRows.Select(r => new StructuredLogEntry(
            ParseDateTime(r, 0),
            NormalizeSeverity(GetString(r, 1)),
            NullIfEmpty(GetString(r, 2)) ?? "—",
            NullIfEmpty(GetString(r, 3)) ?? "—",
            NullIfEmpty(GetString(r, 4)) ?? "—",
            NullIfEmpty(GetString(r, 5)) ?? "—",
            ParseStringDictionary(r, 6))).ToList();

        var totalEvents = counts.Debug + counts.Info + counts.Warn + counts.Error;

        return new StructuredLogsResponse(true, totalEvents, counts, volume, entries);
    }

    public async Task<AiPerformanceResponse> GetAiPerformanceAsync(
        string timeRange, CancellationToken cancellationToken = default)
    {
        var (appId, apiKey) = await GetCredentialsAsync(cancellationToken);
        if (appId is null || apiKey is null)
            return new AiPerformanceResponse(false, null, null, null, [], null, [], [], [], [], [], null);

        var range = ParseTimeRange(timeRange);

        // ── Server-side metrics from AiCallCompleted structured logs ───
        // Emitted by TelemetryChatClient (Aonik.Ai.Observability) as the
        // outermost IChatClient decorator, so EVERY LLM call lands here —
        // chat endpoint, summariser, projector, agent tools. Properties
        // flow through OTel into customDimensions on the traces table.
        //
        // The legacy `AguiRunCompleted` log is still emitted by the AG-UI
        // streaming endpoint but is now a strict subset of AiCallCompleted.
        // The per-agent panel groups by `UseCase` instead of `AgentName`
        // so background callers (e.g. conversation.summary) show up too.

        var latencyDistTask = CachedQueryAsync(
            $"observability:aiPerf:latencyDist:{timeRange}", appId, apiKey,
            $"traces | where timestamp > {range.Ago} | where message startswith \"AiCallCompleted\" | extend latencyMs = todouble(customDimensions[\"LatencyMs\"]) | where isnotempty(latencyMs) | summarize p50=percentile(latencyMs, 50), p75=percentile(latencyMs, 75), p90=percentile(latencyMs, 90), p95=percentile(latencyMs, 95), p99=percentile(latencyMs, 99)",
            cancellationToken);

        var ttftDistTask = CachedQueryAsync(
            $"observability:aiPerf:ttftDist:{timeRange}", appId, apiKey,
            $"traces | where timestamp > {range.Ago} | where message startswith \"AiCallCompleted\" | extend ttftMs = todouble(customDimensions[\"TtftMs\"]) | where isnotempty(ttftMs) | summarize p50=percentile(ttftMs, 50), p75=percentile(ttftMs, 75), p90=percentile(ttftMs, 90), p95=percentile(ttftMs, 95), p99=percentile(ttftMs, 99)",
            cancellationToken);

        var tokenUsageTask = CachedQueryAsync(
            $"observability:aiPerf:tokenUsage:{timeRange}", appId, apiKey,
            $"traces | where timestamp > {range.Ago} | where message startswith \"AiCallCompleted\" | extend inputTokens = tolong(customDimensions[\"InputTokens\"]), outputTokens = tolong(customDimensions[\"OutputTokens\"]) | summarize totalInput=sum(inputTokens), totalOutput=sum(outputTokens), avgInput=avg(todouble(inputTokens)), avgOutput=avg(todouble(outputTokens)), runs=count()",
            cancellationToken);

        // ByAgent stays sourced from AguiRunCompleted (chat-only) so the
        // agent-specific workspace panels keep their per-agent grouping.
        var byAgentTask = CachedQueryAsync(
            $"observability:aiPerf:byAgent:{timeRange}", appId, apiKey,
            $"traces | where timestamp > {range.Ago} | where message startswith \"AguiRunCompleted\" | extend agentName = tostring(customDimensions[\"AgentName\"]), latencyMs = todouble(customDimensions[\"LatencyMs\"]), ttftMs = todouble(customDimensions[\"TtftMs\"]), inputTokens = tolong(customDimensions[\"InputTokens\"]), outputTokens = tolong(customDimensions[\"OutputTokens\"]) | summarize runs=count(), avgLatency=avg(latencyMs), p95Latency=percentile(latencyMs, 95), avgTtft=avg(ttftMs), p95Ttft=percentile(ttftMs, 95), totalInput=sum(inputTokens), totalOutput=sum(outputTokens) by agentName | order by runs desc",
            cancellationToken);

        // ByUseCase covers EVERY LLM call from the new TelemetryChatClient
        // — including background summariser/projector/tool calls that AG-UI
        // never sees. Sums EstimatedCostUsd for the AI Spend card.
        var byUseCaseTask = CachedQueryAsync(
            $"observability:aiPerf:byUseCase:{timeRange}", appId, apiKey,
            $"traces | where timestamp > {range.Ago} | where message startswith \"AiCallCompleted\" | extend useCase = tostring(customDimensions[\"UseCase\"]), latencyMs = todouble(customDimensions[\"LatencyMs\"]), ttftMs = todouble(customDimensions[\"TtftMs\"]), inputTokens = tolong(customDimensions[\"InputTokens\"]), outputTokens = tolong(customDimensions[\"OutputTokens\"]), costUsd = todouble(customDimensions[\"EstimatedCostUsd\"]) | summarize calls=count(), avgLatency=avg(latencyMs), p95Latency=percentile(latencyMs, 95), avgTtft=avg(ttftMs), p95Ttft=percentile(ttftMs, 95), totalInput=sum(inputTokens), totalOutput=sum(outputTokens), totalCost=sum(costUsd) by useCase | order by calls desc",
            cancellationToken);

        // ByModel — the same AiCallCompleted firehose grouped by the model
        // actually used (falling back to the requested model when the
        // provider does not echo one back). Powers the per-model panel and
        // is the natural drill-down from the AI Spend card.
        var byModelTask = CachedQueryAsync(
            $"observability:aiPerf:byModel:{timeRange}", appId, apiKey,
            $"traces | where timestamp > {range.Ago} | where message startswith \"AiCallCompleted\" | extend actualModel = tostring(customDimensions[\"ActualModel\"]), requestedModel = tostring(customDimensions[\"RequestedModel\"]), latencyMs = todouble(customDimensions[\"LatencyMs\"]), inputTokens = tolong(customDimensions[\"InputTokens\"]), outputTokens = tolong(customDimensions[\"OutputTokens\"]), costUsd = todouble(customDimensions[\"EstimatedCostUsd\"]) | extend model = iff(isempty(actualModel), requestedModel, actualModel) | extend model = iff(isempty(model), \"unknown\", model) | summarize calls=count(), avgLatency=avg(latencyMs), p95Latency=percentile(latencyMs, 95), totalInput=sum(inputTokens), totalOutput=sum(outputTokens), totalCost=sum(costUsd) by model | order by calls desc",
            cancellationToken);

        // ── Client-side metrics from ChatClientMetrics structured logs ──

        var clientServerTask = CachedQueryAsync(
            $"observability:aiPerf:clientServer:{timeRange}", appId, apiKey,
            $"traces | where timestamp > {range.Ago} | where message startswith \"ChatClientMetrics\" | extend clientRt = todouble(customDimensions[\"ClientRoundTripMs\"]), serverLat = todouble(customDimensions[\"ServerLatencyMs\"]), clientTtft = todouble(customDimensions[\"ClientTtftMs\"]), serverTtft = todouble(customDimensions[\"ServerTtftMs\"]) | summarize avgClientRt=avg(clientRt), avgServerLat=avg(serverLat), avgClientTtft=avg(clientTtft), avgServerTtft=avg(serverTtft)",
            cancellationToken);

        // ── Time series ─────────────────────────────────────────────────

        var latencyTsTask = CachedQueryAsync(
            $"observability:aiPerf:latencyTs:{timeRange}", appId, apiKey,
            $"traces | where timestamp > {range.Ago} | where message startswith \"AiCallCompleted\" | extend latencyMs = todouble(customDimensions[\"LatencyMs\"]) | summarize avg(latencyMs) by bin(timestamp, {range.Bin}) | order by timestamp asc",
            cancellationToken);

        var ttftTsTask = CachedQueryAsync(
            $"observability:aiPerf:ttftTs:{timeRange}", appId, apiKey,
            $"traces | where timestamp > {range.Ago} | where message startswith \"AiCallCompleted\" | extend ttftMs = todouble(customDimensions[\"TtftMs\"]) | summarize avg(ttftMs) by bin(timestamp, {range.Bin}) | order by timestamp asc",
            cancellationToken);

        var tokenTsTask = CachedQueryAsync(
            $"observability:aiPerf:tokenTs:{timeRange}", appId, apiKey,
            $"traces | where timestamp > {range.Ago} | where message startswith \"AiCallCompleted\" | extend totalTokens = tolong(customDimensions[\"TotalTokens\"]) | summarize sum(totalTokens) by bin(timestamp, {range.Bin}) | order by timestamp asc",
            cancellationToken);

        // ── Personal finance streaming diagnostics ──────────────────────

        const string pfAgentName = "personal-finance-agent";

        var pfPhaseTask = CachedQueryAsync(
            $"observability:aiPerf:pfStreaming:phases:{timeRange}", appId, apiKey,
            $"traces | where timestamp > {range.Ago} | where message startswith \"AguiRunPhases\" | extend agentName = tostring(customDimensions[\"AgentName\"]) | where agentName == \"{pfAgentName}\" | extend historySource = tostring(customDimensions[\"HistorySource\"]), requestToFirstTokenMs = todouble(customDimensions[\"RequestToFirstTokenMs\"]), userBriefDurationMs = todouble(customDimensions[\"UserBriefDurationMs\"]), historyDurationMs = todouble(customDimensions[\"HistoryDurationMs\"]), requestToRunStartedSseMs = todouble(customDimensions[\"RequestToRunStartedSseMs\"]), requestToFirstTokenSseMs = todouble(customDimensions[\"RequestToFirstTokenSseMs\"]) | extend historyLoadMs = iff(historySource == \"client\", real(null), historyDurationMs) | summarize requestToFirstTokenP50=percentile(requestToFirstTokenMs, 50), requestToFirstTokenP95=percentile(requestToFirstTokenMs, 95), requestToFirstTokenP99=percentile(requestToFirstTokenMs, 99), requestToFirstTokenSamples=countif(isnotnull(requestToFirstTokenMs)), userBriefP50=percentile(userBriefDurationMs, 50), userBriefP95=percentile(userBriefDurationMs, 95), userBriefP99=percentile(userBriefDurationMs, 99), userBriefSamples=countif(isnotnull(userBriefDurationMs)), historyLoadP50=percentile(historyLoadMs, 50), historyLoadP95=percentile(historyLoadMs, 95), historyLoadP99=percentile(historyLoadMs, 99), historyLoadSamples=countif(isnotnull(historyLoadMs)), runStartedSseP50=percentile(requestToRunStartedSseMs, 50), runStartedSseP95=percentile(requestToRunStartedSseMs, 95), runStartedSseP99=percentile(requestToRunStartedSseMs, 99), runStartedSseSamples=countif(isnotnull(requestToRunStartedSseMs)), firstTokenSseP50=percentile(requestToFirstTokenSseMs, 50), firstTokenSseP95=percentile(requestToFirstTokenSseMs, 95), firstTokenSseP99=percentile(requestToFirstTokenSseMs, 99), firstTokenSseSamples=countif(isnotnull(requestToFirstTokenSseMs))",
            cancellationToken);

        var pfCacheTask = CachedQueryAsync(
            $"observability:aiPerf:pfStreaming:caches:{timeRange}", appId, apiKey,
            $"traces | where timestamp > {range.Ago} | where message startswith \"AguiRunPhases\" | extend agentName = tostring(customDimensions[\"AgentName\"]) | where agentName == \"{pfAgentName}\" | summarize userBriefHits=countif(tostring(customDimensions[\"UserBriefCacheStatus\"]) == \"hit\"), userBriefMisses=countif(tostring(customDimensions[\"UserBriefCacheStatus\"]) == \"miss\"), historyHits=countif(tostring(customDimensions[\"HistorySource\"]) == \"cache\"), historyMisses=countif(tostring(customDimensions[\"HistorySource\"]) == \"db\")",
            cancellationToken);

        var pfThreadModesTask = CachedQueryAsync(
            $"observability:aiPerf:pfStreaming:threadModes:{timeRange}", appId, apiKey,
            $"traces | where timestamp > {range.Ago} | where message startswith \"AguiRunPhases\" | extend agentName = tostring(customDimensions[\"AgentName\"]), requestToFirstTokenMs = todouble(customDimensions[\"RequestToFirstTokenMs\"]) | where agentName == \"{pfAgentName}\" and isnotnull(requestToFirstTokenMs) | extend mode = iff(tobool(customDimensions[\"IsNewThread\"]), \"new-thread\", \"existing-thread\") | summarize runs=count(), avgRequestToFirstToken=avg(requestToFirstTokenMs), p95RequestToFirstToken=percentile(requestToFirstTokenMs, 95) by mode | order by mode asc",
            cancellationToken);

        var pfHistorySourcesTask = CachedQueryAsync(
            $"observability:aiPerf:pfStreaming:historySources:{timeRange}", appId, apiKey,
            $"traces | where timestamp > {range.Ago} | where message startswith \"AguiRunPhases\" | extend agentName = tostring(customDimensions[\"AgentName\"]), requestToFirstTokenMs = todouble(customDimensions[\"RequestToFirstTokenMs\"]), historySource = tostring(customDimensions[\"HistorySource\"]) | where agentName == \"{pfAgentName}\" and isnotnull(requestToFirstTokenMs) and historySource in (\"client\", \"cache\", \"db\") | summarize runs=count(), avgRequestToFirstToken=avg(requestToFirstTokenMs), p95RequestToFirstToken=percentile(requestToFirstTokenMs, 95) by historySource | order by historySource asc",
            cancellationToken);

        var pfRequestToFirstTokenTsTask = CachedQueryAsync(
            $"observability:aiPerf:pfStreaming:reqToFirstTokenTs:{timeRange}", appId, apiKey,
            $"traces | where timestamp > {range.Ago} | where message startswith \"AguiRunPhases\" | extend agentName = tostring(customDimensions[\"AgentName\"]), requestToFirstTokenMs = todouble(customDimensions[\"RequestToFirstTokenMs\"]) | where agentName == \"{pfAgentName}\" and isnotnull(requestToFirstTokenMs) | summarize avg(requestToFirstTokenMs) by bin(timestamp, {range.Bin}) | order by timestamp asc",
            cancellationToken);

        var pfUserBriefTsTask = CachedQueryAsync(
            $"observability:aiPerf:pfStreaming:userBriefTs:{timeRange}", appId, apiKey,
            $"traces | where timestamp > {range.Ago} | where message startswith \"AguiRunPhases\" | extend agentName = tostring(customDimensions[\"AgentName\"]), userBriefDurationMs = todouble(customDimensions[\"UserBriefDurationMs\"]) | where agentName == \"{pfAgentName}\" and isnotnull(userBriefDurationMs) | summarize avg(userBriefDurationMs) by bin(timestamp, {range.Bin}) | order by timestamp asc",
            cancellationToken);

        var pfHistoryTsTask = CachedQueryAsync(
            $"observability:aiPerf:pfStreaming:historyTs:{timeRange}", appId, apiKey,
            $"traces | where timestamp > {range.Ago} | where message startswith \"AguiRunPhases\" | extend agentName = tostring(customDimensions[\"AgentName\"]), historyDurationMs = todouble(customDimensions[\"HistoryDurationMs\"]), historySource = tostring(customDimensions[\"HistorySource\"]) | where agentName == \"{pfAgentName}\" and historySource != \"client\" and isnotnull(historyDurationMs) | summarize avg(historyDurationMs) by bin(timestamp, {range.Bin}) | order by timestamp asc",
            cancellationToken);

        var pfRunStartedTsTask = CachedQueryAsync(
            $"observability:aiPerf:pfStreaming:runStartedTs:{timeRange}", appId, apiKey,
            $"traces | where timestamp > {range.Ago} | where message startswith \"AguiRunPhases\" | extend agentName = tostring(customDimensions[\"AgentName\"]), requestToRunStartedSseMs = todouble(customDimensions[\"RequestToRunStartedSseMs\"]) | where agentName == \"{pfAgentName}\" and isnotnull(requestToRunStartedSseMs) | summarize avg(requestToRunStartedSseMs) by bin(timestamp, {range.Bin}) | order by timestamp asc",
            cancellationToken);

        await Task.WhenAll(latencyDistTask, ttftDistTask, tokenUsageTask,
            byAgentTask, byUseCaseTask, byModelTask,
            clientServerTask, latencyTsTask, ttftTsTask, tokenTsTask,
            pfPhaseTask, pfCacheTask, pfThreadModesTask, pfHistorySourcesTask,
            pfRequestToFirstTokenTsTask, pfUserBriefTsTask, pfHistoryTsTask, pfRunStartedTsTask);

        // ── Parse latency distribution ──────────────────────────────────

        var latencyDistRows = await latencyDistTask;
        AiLatencyDistribution? latency = null;
        if (latencyDistRows.Count > 0)
        {
            latency = new AiLatencyDistribution(
                Math.Round(ParseDouble(latencyDistRows[0], 0), 2),
                Math.Round(ParseDouble(latencyDistRows[0], 1), 2),
                Math.Round(ParseDouble(latencyDistRows[0], 2), 2),
                Math.Round(ParseDouble(latencyDistRows[0], 3), 2),
                Math.Round(ParseDouble(latencyDistRows[0], 4), 2));
        }

        // ── Parse TTFT distribution ─────────────────────────────────────

        var ttftDistRows = await ttftDistTask;
        AiTtftDistribution? ttft = null;
        if (ttftDistRows.Count > 0)
        {
            ttft = new AiTtftDistribution(
                Math.Round(ParseDouble(ttftDistRows[0], 0), 2),
                Math.Round(ParseDouble(ttftDistRows[0], 1), 2),
                Math.Round(ParseDouble(ttftDistRows[0], 2), 2),
                Math.Round(ParseDouble(ttftDistRows[0], 3), 2),
                Math.Round(ParseDouble(ttftDistRows[0], 4), 2));
        }

        // ── Parse token usage ───────────────────────────────────────────

        var tokenRows = await tokenUsageTask;
        AiTokenUsage? tokenUsage = null;
        if (tokenRows.Count > 0)
        {
            var totalInput = (long)ParseDouble(tokenRows[0], 0);
            var totalOutput = (long)ParseDouble(tokenRows[0], 1);
            tokenUsage = new AiTokenUsage(
                totalInput, totalOutput, totalInput + totalOutput,
                Math.Round(ParseDouble(tokenRows[0], 2), 1),
                Math.Round(ParseDouble(tokenRows[0], 3), 1));
        }

        // ── Parse per-agent breakdown ───────────────────────────────────

        var agentRows = await byAgentTask;
        var byAgent = agentRows.Select(r => new AiAgentPerformance(
            GetString(r, 0),
            (long)ParseDouble(r, 1),
            Math.Round(ParseDouble(r, 2), 2),
            Math.Round(ParseDouble(r, 3), 2),
            Math.Round(ParseDouble(r, 4), 2),
            Math.Round(ParseDouble(r, 5), 2),
            (long)ParseDouble(r, 6),
            (long)ParseDouble(r, 7))).ToList();

        // ── Parse per-use-case breakdown ────────────────────────────────

        var useCaseRows = await byUseCaseTask;
        var byUseCase = useCaseRows.Select(r => new AiUseCasePerformance(
            GetString(r, 0),
            (long)ParseDouble(r, 1),
            Math.Round(ParseDouble(r, 2), 2),
            Math.Round(ParseDouble(r, 3), 2),
            Math.Round(ParseDouble(r, 4), 2),
            Math.Round(ParseDouble(r, 5), 2),
            (long)ParseDouble(r, 6),
            (long)ParseDouble(r, 7),
            Math.Round(ParseDouble(r, 8), 4))).ToList();

        // ── Parse per-model breakdown ───────────────────────────────────

        var modelRows = await byModelTask;
        var byModel = modelRows.Select(r => new AiModelPerformance(
            GetString(r, 0),
            (long)ParseDouble(r, 1),
            Math.Round(ParseDouble(r, 2), 2),
            Math.Round(ParseDouble(r, 3), 2),
            (long)ParseDouble(r, 4),
            (long)ParseDouble(r, 5),
            Math.Round(ParseDouble(r, 6), 4))).ToList();

        // ── Parse client vs server comparison ───────────────────────────

        var csRows = await clientServerTask;
        AiClientServerComparison? clientServer = null;
        if (csRows.Count > 0)
        {
            var avgClientRt = Math.Round(ParseDouble(csRows[0], 0), 2);
            var avgServerLat = Math.Round(ParseDouble(csRows[0], 1), 2);
            clientServer = new AiClientServerComparison(
                avgClientRt, avgServerLat,
                Math.Round(avgClientRt - avgServerLat, 2),
                Math.Round(ParseDouble(csRows[0], 2), 2),
                Math.Round(ParseDouble(csRows[0], 3), 2));
        }

        // ── Parse time series ───────────────────────────────────────────

        var latencyTs = (await latencyTsTask).Select(r => new TimeSeriesPoint(
            ParseDateTime(r, 0), Math.Round(ParseDouble(r, 1), 2))).ToList();

        var ttftTs = (await ttftTsTask).Select(r => new TimeSeriesPoint(
            ParseDateTime(r, 0), Math.Round(ParseDouble(r, 1), 2))).ToList();

        var tokenTs = (await tokenTsTask).Select(r => new TimeSeriesPoint(
            ParseDateTime(r, 0), ParseDouble(r, 1))).ToList();

        // ── Parse personal finance streaming diagnostics ────────────────

        PersonalFinanceStreamingDiagnostics? personalFinanceStreaming = null;

        var pfPhaseRows = await pfPhaseTask;
        var pfCacheRows = await pfCacheTask;
        var pfThreadModeRows = await pfThreadModesTask;
        var pfHistorySourceRows = await pfHistorySourcesTask;

        if (pfPhaseRows.Count > 0 || pfCacheRows.Count > 0 || pfThreadModeRows.Count > 0 || pfHistorySourceRows.Count > 0)
        {
            var phases = new List<AiStreamingPhaseMetric>();
            if (pfPhaseRows.Count > 0)
            {
                var row = pfPhaseRows[0];
                phases.Add(new AiStreamingPhaseMetric(
                    "request_to_first_token",
                    Math.Round(ParseDouble(row, 0), 2),
                    Math.Round(ParseDouble(row, 1), 2),
                    Math.Round(ParseDouble(row, 2), 2),
                    (long)ParseDouble(row, 3)));
                phases.Add(new AiStreamingPhaseMetric(
                    "user_brief",
                    Math.Round(ParseDouble(row, 4), 2),
                    Math.Round(ParseDouble(row, 5), 2),
                    Math.Round(ParseDouble(row, 6), 2),
                    (long)ParseDouble(row, 7)));
                phases.Add(new AiStreamingPhaseMetric(
                    "history_load",
                    Math.Round(ParseDouble(row, 8), 2),
                    Math.Round(ParseDouble(row, 9), 2),
                    Math.Round(ParseDouble(row, 10), 2),
                    (long)ParseDouble(row, 11)));
                phases.Add(new AiStreamingPhaseMetric(
                    "run_started_sse",
                    Math.Round(ParseDouble(row, 12), 2),
                    Math.Round(ParseDouble(row, 13), 2),
                    Math.Round(ParseDouble(row, 14), 2),
                    (long)ParseDouble(row, 15)));
                phases.Add(new AiStreamingPhaseMetric(
                    "first_token_sse",
                    Math.Round(ParseDouble(row, 16), 2),
                    Math.Round(ParseDouble(row, 17), 2),
                    Math.Round(ParseDouble(row, 18), 2),
                    (long)ParseDouble(row, 19)));
            }

            var caches = new List<AiStreamingCacheMetric>();
            if (pfCacheRows.Count > 0)
            {
                var row = pfCacheRows[0];
                var userBriefHits = (long)ParseDouble(row, 0);
                var userBriefMisses = (long)ParseDouble(row, 1);
                var historyHits = (long)ParseDouble(row, 2);
                var historyMisses = (long)ParseDouble(row, 3);

                caches.Add(new AiStreamingCacheMetric(
                    "user_brief",
                    userBriefHits,
                    userBriefMisses,
                    ComputeHitRate(userBriefHits, userBriefMisses)));
                caches.Add(new AiStreamingCacheMetric(
                    "history",
                    historyHits,
                    historyMisses,
                    ComputeHitRate(historyHits, historyMisses)));
            }

            var threadModes = pfThreadModeRows.Select(r => new AiStreamingModeMetric(
                GetString(r, 0),
                (long)ParseDouble(r, 1),
                Math.Round(ParseDouble(r, 2), 2),
                Math.Round(ParseDouble(r, 3), 2))).ToList();

            var historySources = pfHistorySourceRows.Select(r => new AiStreamingModeMetric(
                GetString(r, 0),
                (long)ParseDouble(r, 1),
                Math.Round(ParseDouble(r, 2), 2),
                Math.Round(ParseDouble(r, 3), 2))).ToList();

            // Each of the four phase time-series tasks has already completed
            // (via the WhenAll above), so the awaits here are fast-path
            // unwraps with no thread blocking.
            var requestToFirstTokenRows = await pfRequestToFirstTokenTsTask;
            var userBriefRows = await pfUserBriefTsTask;
            var historyRows = await pfHistoryTsTask;
            var runStartedRows = await pfRunStartedTsTask;

            var phaseTimeSeries = new List<AiStreamingPhaseTimeSeries>
            {
                new(
                    "request_to_first_token",
                    requestToFirstTokenRows.Select(r => new TimeSeriesPoint(
                        ParseDateTime(r, 0), Math.Round(ParseDouble(r, 1), 2))).ToList()),
                new(
                    "user_brief",
                    userBriefRows.Select(r => new TimeSeriesPoint(
                        ParseDateTime(r, 0), Math.Round(ParseDouble(r, 1), 2))).ToList()),
                new(
                    "history_load",
                    historyRows.Select(r => new TimeSeriesPoint(
                        ParseDateTime(r, 0), Math.Round(ParseDouble(r, 1), 2))).ToList()),
                new(
                    "run_started_sse",
                    runStartedRows.Select(r => new TimeSeriesPoint(
                        ParseDateTime(r, 0), Math.Round(ParseDouble(r, 1), 2))).ToList()),
            };

            personalFinanceStreaming = new PersonalFinanceStreamingDiagnostics(
                pfAgentName,
                phases,
                caches,
                threadModes,
                historySources,
                phaseTimeSeries);
        }

        return new AiPerformanceResponse(true, latency, ttft, tokenUsage,
            byAgent, clientServer, latencyTs, ttftTs, tokenTs, byUseCase, byModel,
            personalFinanceStreaming);
    }

    // ── Retrieval (Qdrant + embedding) ──────────────────────────────
    //
    // Metrics come from the Aonik.VectorStore meter exported via OTel.
    // App Insights stores histogram aggregates on `customMetrics` with
    // name=<instrument>, valueCount/valueSum/valueMin/valueMax per bucket.
    // Activities land on `dependencies` (type=InProc) with span attrs
    // under customDimensions (collection, result_count, error_type).

    public async Task<RetrievalResponse> GetRetrievalAsync(
        string timeRange, CancellationToken cancellationToken = default)
    {
        var (appId, apiKey) = await GetCredentialsAsync(cancellationToken);
        if (appId is null || apiKey is null)
            return new RetrievalResponse(false, [], [], 0, 0, 0, 0, [], []);

        var range = ParseTimeRange(timeRange);

        // Latency histograms — one row per instrument. valueSum/valueCount
        // gives us avg; percentile() against valueCount-weighted rows is
        // an approximation (AppInsights doesn't store raw samples), but
        // it's the same approximation we already use elsewhere.
        var latenciesTask = CachedQueryAsync(
            $"observability:retrieval:latencies:{timeRange}", appId, apiKey,
            $"customMetrics | where timestamp > {range.Ago} | where name in (\"qdrant.vector.upsert.duration_ms\", \"qdrant.vector.search.duration_ms\", \"embedding.api.duration_ms\") | summarize samples=sum(valueCount), totalSum=sum(valueSum), p50=percentile(value, 50), p95=percentile(value, 95), p99=percentile(value, 99) by name",
            cancellationToken);

        // Per-collection Qdrant search stats from dependencies table
        // (StartActivity spans surface as dependencies type=InProc).
        var collectionsTask = CachedQueryAsync(
            $"observability:retrieval:collections:{timeRange}", appId, apiKey,
            $"dependencies | where timestamp > {range.Ago} | where name == \"qdrant.search\" | extend collection = tostring(customDimensions[\"collection\"]), resultCount = toint(customDimensions[\"result_count\"]) | summarize searches=count(), avgResults=avg(todouble(resultCount)), emptySearches=countif(resultCount == 0), avgLatency=avg(duration), p95Latency=percentile(duration, 95) by collection | order by searches desc",
            cancellationToken);

        // Counters — embedding error count and totals.
        var errorsTask = CachedQueryAsync(
            $"observability:retrieval:errors:{timeRange}", appId, apiKey,
            $"customMetrics | where timestamp > {range.Ago} | where name == \"embedding.api.error_count\" | summarize errors=sum(valueSum)",
            cancellationToken);

        var totalsTask = CachedQueryAsync(
            $"observability:retrieval:totals:{timeRange}", appId, apiKey,
            $"customMetrics | where timestamp > {range.Ago} | where name in (\"qdrant.vector.search.duration_ms\", \"qdrant.vector.upsert.duration_ms\", \"embedding.api.duration_ms\") | summarize total=sum(valueCount) by name",
            cancellationToken);

        var searchTsTask = CachedQueryAsync(
            $"observability:retrieval:searchTs:{timeRange}", appId, apiKey,
            $"customMetrics | where timestamp > {range.Ago} | where name == \"qdrant.vector.search.duration_ms\" | summarize avg(value) by bin(timestamp, {range.Bin}) | order by timestamp asc",
            cancellationToken);

        var embeddingTsTask = CachedQueryAsync(
            $"observability:retrieval:embeddingTs:{timeRange}", appId, apiKey,
            $"customMetrics | where timestamp > {range.Ago} | where name == \"embedding.api.duration_ms\" | summarize avg(value) by bin(timestamp, {range.Bin}) | order by timestamp asc",
            cancellationToken);

        await Task.WhenAll(latenciesTask, collectionsTask, errorsTask, totalsTask,
            searchTsTask, embeddingTsTask);

        // All six tasks have completed via WhenAll above; awaiting each is
        // a fast-path unwrap that avoids both AggregateException wrapping
        // and any sync blocking that .Result would have caused.
        var latencyRows = await latenciesTask;
        var collectionRows = await collectionsTask;
        var errorRows = await errorsTask;
        var totalsRows = await totalsTask;
        var searchTsRows = await searchTsTask;
        var embeddingTsRows = await embeddingTsTask;

        var latencies = latencyRows.Select(r =>
        {
            var samples = (long)ParseDouble(r, 1);
            var totalSum = ParseDouble(r, 2);
            return new RetrievalLatency(
                GetString(r, 0),
                samples,
                samples > 0 ? Math.Round(totalSum / samples, 2) : 0,
                Math.Round(ParseDouble(r, 3), 2),
                Math.Round(ParseDouble(r, 4), 2),
                Math.Round(ParseDouble(r, 5), 2));
        }).ToList();

        var collections = collectionRows.Select(r => new RetrievalCollectionStats(
            GetString(r, 0),
            (long)ParseDouble(r, 1),
            Math.Round(ParseDouble(r, 2), 2),
            (long)ParseDouble(r, 3),
            Math.Round(ParseDouble(r, 4), 2),
            Math.Round(ParseDouble(r, 5), 2))).ToList();

        var embeddingErrors = errorRows.Count > 0
            ? (long)ParseDouble(errorRows[0], 0) : 0;

        long totalSearches = 0, totalUpserts = 0, totalEmbeddingCalls = 0;
        foreach (var row in totalsRows)
        {
            var name = GetString(row, 0);
            var count = (long)ParseDouble(row, 1);
            if (name == "qdrant.vector.search.duration_ms") totalSearches = count;
            else if (name == "qdrant.vector.upsert.duration_ms") totalUpserts = count;
            else if (name == "embedding.api.duration_ms") totalEmbeddingCalls = count;
        }

        var searchTs = searchTsRows.Select(r => new TimeSeriesPoint(
            ParseDateTime(r, 0), Math.Round(ParseDouble(r, 1), 2))).ToList();

        var embeddingTs = embeddingTsRows.Select(r => new TimeSeriesPoint(
            ParseDateTime(r, 0), Math.Round(ParseDouble(r, 1), 2))).ToList();

        return new RetrievalResponse(
            true, latencies, collections, embeddingErrors,
            totalSearches, totalUpserts, totalEmbeddingCalls,
            searchTs, embeddingTs);
    }

    // ── Topology ────────────────────────────────────────────────────
    //
    // The topology graph is assembled from two signals that App Insights
    // already collects by default:
    //   - `requests`      — every inbound HTTP request, tagged with
    //                       cloud_RoleName (= the ACA container app name).
    //   - `dependencies`  — every outbound call (SQL, HTTP, Azure SDK,
    //                       InProc activity), same cloud_RoleName + the
    //                       `target` / `type` of the callee.
    //
    // A service node therefore = any cloud_RoleName that appeared as a
    // caller. A dependency node = any `target` that appeared as callee.
    // Edges are the (caller, target, type) tuples with rollup stats.
    //
    // Health rules: critical if error rate > 10% OR p95 > 5s;
    //               degraded if error rate > 2% OR p95 > 1.5s;
    //               healthy otherwise.

    public async Task<TopologyResponse> GetTopologyAsync(
        string timeRange, CancellationToken cancellationToken = default)
    {
        var runtimeStatuses = await _runtimeOperationsService.ListRuntimeServicesAsync(cancellationToken);
        var runtimeByAppName = runtimeStatuses.ToDictionary(
            status => status.ServiceName,
            status => status,
            StringComparer.OrdinalIgnoreCase);

        var (appId, apiKey) = await GetCredentialsAsync(cancellationToken);
        if (appId is null || apiKey is null)
        {
            var runtimeOnlyNodes = runtimeStatuses
                .Select(status => CreateRuntimeTopologyNode(status, calls: 0, errorRatePct: 0, p95LatencyMs: 0, lastSeen: status.LastActiveTime))
                .ToList();

            return new TopologyResponse(false, runtimeOnlyNodes, [], DateTime.UtcNow);
        }

        var range = ParseTimeRange(timeRange);

        var servicesTask = CachedQueryAsync(
            $"observability:topology:services:{timeRange}", appId, apiKey,
            $"requests | where timestamp > {range.Ago} | summarize calls=count(), failures=countif(success == false), p95=percentile(duration, 95), lastSeen=max(timestamp) by service=cloud_RoleName | order by calls desc",
            cancellationToken);

        var edgesTask = CachedQueryAsync(
            $"observability:topology:edges:{timeRange}", appId, apiKey,
            $"dependencies | where timestamp > {range.Ago} | where isnotempty(cloud_RoleName) and isnotempty(target) | summarize calls=count(), failures=countif(success == false), p95=percentile(duration, 95), lastSeen=max(timestamp) by source=cloud_RoleName, target, type | order by calls desc | take 200",
            cancellationToken);

        await Task.WhenAll(servicesTask, edgesTask);

        var serviceRows = await servicesTask;
        var edgeRows = await edgesTask;

        var nodes = new Dictionary<string, TopologyNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in serviceRows)
        {
            var id = GetString(r, 0);
            if (string.IsNullOrWhiteSpace(id)) continue;

            var calls = (long)ParseDouble(r, 1);
            var failures = (long)ParseDouble(r, 2);
            var errorRate = calls > 0 ? (double)failures / calls * 100 : 0;
            var p95 = ParseDouble(r, 3);
            var lastSeen = ParseDateTime(r, 4);

            nodes[id] = new TopologyNode(
                id,
                PrettifyServiceName(id),
                "service",
                ClassifyHealth(errorRate, p95),
                calls,
                Math.Round(errorRate, 2),
                Math.Round(p95, 2),
                lastSeen == DateTime.MinValue ? null : lastSeen,
                TryMapRuntimeStatus(id, runtimeByAppName));
        }

        var edges = new List<TopologyEdge>();
        foreach (var r in edgeRows)
        {
            var source = GetString(r, 0);
            var target = GetString(r, 1);
            var type = GetString(r, 2);
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
                continue;

            var calls = (long)ParseDouble(r, 3);
            var failures = (long)ParseDouble(r, 4);
            var errorRate = calls > 0 ? (double)failures / calls * 100 : 0;
            var p95 = ParseDouble(r, 5);
            var lastSeen = ParseDateTime(r, 6);

            // Register the target as a node if we haven't already.
            var targetId = $"ext:{target}";
            if (!nodes.ContainsKey(targetId))
            {
                nodes[targetId] = new TopologyNode(
                    targetId,
                    target,
                    ClassifyTargetKind(type, target),
                    ClassifyHealth(errorRate, p95),
                    calls,
                    Math.Round(errorRate, 2),
                    Math.Round(p95, 2),
                    lastSeen == DateTime.MinValue ? null : lastSeen,
                    null);
            }

            edges.Add(new TopologyEdge(
                source, targetId,
                NormaliseEdgeKind(type),
                calls,
                Math.Round(errorRate, 2),
                Math.Round(p95, 2)));
        }

        foreach (var runtime in runtimeStatuses)
        {
            if (nodes.ContainsKey(runtime.ServiceName))
            {
                continue;
            }

            nodes[runtime.ServiceName] = CreateRuntimeTopologyNode(runtime, calls: 0, errorRatePct: 0, p95LatencyMs: 0, lastSeen: runtime.LastActiveTime);
        }

        return new TopologyResponse(true, [.. nodes.Values], edges, DateTime.UtcNow);
    }

    // ── Money-action trace (Issue #142) ───────────────────────────────

    public async Task<MoneyActionTraceResponse> GetMoneyActionTraceAsync(
        Guid orderId, string timeRange, CancellationToken cancellationToken = default)
    {
        var range = ParseTimeRange(timeRange);

        var (appId, apiKey) = await GetCredentialsAsync(cancellationToken);
        if (appId is null || apiKey is null)
        {
            return new MoneyActionTraceResponse(
                Configured: false,
                OrderId: orderId,
                PricingQuoteId: null,
                TimeRange: timeRange,
                QueryDurationMs: 0,
                Entries: []);
        }

        // Mirrors docs/observability/queries/money-action-by-orderid.kql.
        // orderId is a Guid (safe for literal injection); range.Ago is one
        // of a closed set of "ago(...)" expressions emitted by ParseTimeRange.
        // No untrusted strings reach the KQL.
        //
        // Three-step design (matches saved query):
        //   1. Resolve PricingQuoteId from Confirm-stage log (EventId 1201)
        //      so Quote-stage entries reachable.
        //   2. Direct hits — rows carrying OrderId or PricingQuoteId in
        //      customDimensions.
        //   3. Inherited hits via operation_Id — children of finance spans
        //      (SQL deps, outbound HTTP) that share the trace_id but don't
        //      carry the OrderId tag themselves.
        var kql = $$"""
            let orderId = "{{orderId}}";
            let pricingQuoteId =
                traces
                | where timestamp > {{range.Ago}}
                | where tostring(customDimensions["OrderId"]) == orderId
                | where toint(customDimensions["EventId"]) == 1201
                | extend pq = tostring(customDimensions["PricingQuoteId"])
                | where isnotempty(pq) and pq != "00000000-0000-0000-0000-000000000000"
                | project pq
                | take 1;
            let directHits =
                union
                    (traces       | extend itemType = "trace"),
                    (customEvents | extend itemType = "customEvent"),
                    (dependencies | extend itemType = "dependency"),
                    (exceptions   | extend itemType = "exception")
                | where timestamp > {{range.Ago}}
                | where tostring(customDimensions["OrderId"]) == orderId
                   or tostring(customDimensions["PricingQuoteId"]) in (pricingQuoteId);
            let traceIds =
                directHits
                | where isnotempty(operation_Id)
                | distinct operation_Id;
            let inheritedHits =
                union
                    (traces       | extend itemType = "trace"),
                    (customEvents | extend itemType = "customEvent"),
                    (dependencies | extend itemType = "dependency"),
                    (exceptions   | extend itemType = "exception")
                | where timestamp > {{range.Ago}}
                | where operation_Id in (traceIds);
            union directHits, inheritedHits
            | summarize take_any(*) by itemId
            | project
                timestamp,
                itemType,
                Stage           = tostring(customDimensions["Stage"]),
                Outcome         = tostring(customDimensions["Outcome"]),
                EventId         = tostring(customDimensions["EventId"]),
                name,
                message,
                severityLevel,
                operation_Id,
                PaymentIntentId = tostring(customDimensions["PaymentIntentId"]),
                InvoiceId       = tostring(customDimensions["InvoiceId"]),
                PricingQuoteId  = tostring(customDimensions["PricingQuoteId"]),
                TenantId        = tostring(customDimensions["TenantId"])
            | order by timestamp asc
            """;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var rows = await ExecuteQueryAsync(appId, apiKey, kql, cancellationToken);
        stopwatch.Stop();

        Guid? pricingQuoteId = null;
        var entries = new List<MoneyActionTraceEntry>(rows.Count);
        foreach (var row in rows)
        {
            var entry = new MoneyActionTraceEntry(
                Timestamp: ParseDateTime(row, 0),
                ItemType: GetString(row, 1),
                Stage: NullIfEmpty(GetString(row, 2)),
                Outcome: NullIfEmpty(GetString(row, 3)),
                EventId: ParseNullableInt(row, 4),
                Name: NullIfEmpty(GetString(row, 5)),
                Message: NullIfEmpty(GetString(row, 6)),
                SeverityLevel: ParseNullableInt(row, 7),
                OperationId: NullIfEmpty(GetString(row, 8)),
                PaymentIntentId: ParseNullableGuid(row, 9),
                InvoiceId: ParseNullableGuid(row, 10),
                PricingQuoteId: ParseNullableGuid(row, 11),
                TenantId: ParseNullableGuid(row, 12));

            // First non-null PricingQuoteId becomes the response-envelope
            // join key — the UI can render it and operators can re-query
            // the Quote stage directly without re-running the chain step.
            if (pricingQuoteId is null && entry.PricingQuoteId.HasValue)
            {
                pricingQuoteId = entry.PricingQuoteId.Value;
            }

            entries.Add(entry);
        }

        return new MoneyActionTraceResponse(
            Configured: true,
            OrderId: orderId,
            PricingQuoteId: pricingQuoteId,
            TimeRange: timeRange,
            QueryDurationMs: stopwatch.ElapsedMilliseconds,
            Entries: entries);
    }

    private static TopologyNode CreateRuntimeTopologyNode(
        RuntimeServiceStatus runtime,
        long calls,
        double errorRatePct,
        double p95LatencyMs,
        DateTime? lastSeen)
    {
        return new TopologyNode(
            runtime.ServiceName,
            runtime.DisplayName,
            runtime.ServiceType,
            MapRuntimeStateToTopologyStatus(runtime.RuntimeState),
            calls,
            Math.Round(errorRatePct, 2),
            Math.Round(p95LatencyMs, 2),
            lastSeen,
            runtime);
    }

    private static RuntimeServiceStatus? TryMapRuntimeStatus(
        string nodeId,
        IReadOnlyDictionary<string, RuntimeServiceStatus> runtimeByAppName)
    {
        return runtimeByAppName.TryGetValue(nodeId, out var runtime)
            ? runtime
            : null;
    }

    private static string MapRuntimeStateToTopologyStatus(string runtimeState) =>
        runtimeState switch
        {
            "running" => "healthy",
            "processing" => "degraded",
            "degraded" => "critical",
            "failed" => "critical",
            "scaled-to-zero" => "unknown",
            "stopped" => "unknown",
            "missing" => "unknown",
            _ => "unknown",
        };

    private static string ClassifyHealth(double errorRatePct, double p95Ms) =>
        errorRatePct > 10 || p95Ms > 5000 ? "critical"
        : errorRatePct > 2 || p95Ms > 1500 ? "degraded"
        : "healthy";

    private static string ClassifyTargetKind(string type, string target)
    {
        var t = (type ?? string.Empty).ToLowerInvariant();
        if (t.Contains("sql")) return "datastore";
        if (t.Contains("azure") || t.Contains("storage") || t.Contains("servicebus"))
            return "datastore";
        if (t.Contains("inproc")) return "service";
        if (target.Contains("openai", StringComparison.OrdinalIgnoreCase)
            || target.Contains("anthropic", StringComparison.OrdinalIgnoreCase)
            || target.Contains("googleapis", StringComparison.OrdinalIgnoreCase)
            || target.Contains("auth0", StringComparison.OrdinalIgnoreCase)
            || target.Contains("qdrant", StringComparison.OrdinalIgnoreCase))
            return "external";
        return "external";
    }

    private static string NormaliseEdgeKind(string type)
    {
        var t = (type ?? string.Empty).ToLowerInvariant();
        if (t.Contains("sql")) return "sql";
        if (t.Contains("http")) return "http";
        if (t.Contains("grpc")) return "grpc";
        if (t.Contains("queue") || t.Contains("servicebus")) return "queue";
        if (t.Contains("inproc")) return "event";
        return "http";
    }

    private static string PrettifyServiceName(string roleName) =>
        roleName.Replace("aonik-dev-", "", StringComparison.OrdinalIgnoreCase)
                .Replace("aonik-", "", StringComparison.OrdinalIgnoreCase);

    // ── Credentials ──────────────────────────────────────────────────

    private async Task<(string? AppId, string? ApiKey)> GetCredentialsAsync(
        CancellationToken cancellationToken)
    {
        var appId = await _settingProvider.GetForScopeAsync(
            ObservabilitySettingNames.AppInsightsAppId, SettingScope.Global, cancellationToken: cancellationToken);
        var apiKey = await _settingProvider.GetForScopeAsync(
            ObservabilitySettingNames.AppInsightsApiKey, SettingScope.Global, cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogDebug("Application Insights credentials not configured; returning unconfigured response");
            return (null, null);
        }

        return (appId, apiKey);
    }

    // ── Query execution with caching ─────────────────────────────────

    private async Task<IReadOnlyList<JsonElement[]>> CachedQueryAsync(
        string cacheKey, string appId, string apiKey, string kql,
        CancellationToken cancellationToken)
    {
        return await _cache.GetOrSetAsync(
            cacheKey,
            async ct => await ExecuteQueryAsync(appId, apiKey, kql, ct),
            new FusionCacheEntryOptions(TimeSpan.FromSeconds(60)),
            cancellationToken) ?? [];
    }

    private async Task<IReadOnlyList<JsonElement[]>> ExecuteQueryAsync(
        string appId, string apiKey, string kql, CancellationToken cancellationToken)
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
            _logger.LogWarning(ex, "Failed to call Application Insights API");
            return [];
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Application Insights query returned {StatusCode}: {Query}",
                response.StatusCode, kql);
            return [];
        }

        var body = await response.Content.ReadFromJsonAsync<AppInsightsQueryResponse>(
            JsonOptions, cancellationToken);

        if (body?.Tables is not { Count: > 0 })
            return [];

        var table = body.Tables[0];
        return table.Rows.Select(row =>
            row.Select(cell => cell).ToArray()).ToList();
    }

    // ── Time range helpers ───────────────────────────────────────────

    private static TimeRangeSpec ParseTimeRange(string timeRange) => timeRange switch
    {
        "1h" => new("ago(1h)", "1m", 60),
        "7d" => new("ago(7d)", "1h", 7 * 24 * 60),
        "30d" => new("ago(30d)", "6h", 30 * 24 * 60),
        _ => new("ago(24h)", "15m", 24 * 60), // default "24h"
    };

    private sealed record TimeRangeSpec(string Ago, string Bin, double TotalMinutes);

    // ── Row parsing helpers ──────────────────────────────────────────

    private static DateTime ParseDateTime(JsonElement[] row, int index)
    {
        if (index >= row.Length) return DateTime.MinValue;
        return row[index].ValueKind == JsonValueKind.String
            && DateTime.TryParse(row[index].GetString(), out var dt)
                ? dt
                : DateTime.MinValue;
    }

    private static double ParseDouble(JsonElement[] row, int index)
    {
        if (index >= row.Length) return 0;
        var raw = row[index].ValueKind switch
        {
            JsonValueKind.Number => row[index].GetDouble(),
            JsonValueKind.String when double.TryParse(row[index].GetString(), out var d) => d,
            _ => 0,
        };
        // App Insights avg()/percentile() over all-null groups return NaN.
        // System.Text.Json refuses to serialize NaN/Infinity by default, so
        // collapse those to 0 at the parse boundary to keep the wire JSON clean.
        return double.IsFinite(raw) ? raw : 0;
    }

    private static string GetString(JsonElement[] row, int index)
    {
        if (index >= row.Length) return string.Empty;

        var cell = row[index];
        return cell.ValueKind switch
        {
            JsonValueKind.String => cell.GetString() ?? string.Empty,
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => cell.ToString(),
        };
    }

    private static string? NullIfEmpty(string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;

    private static int? ParseNullableInt(JsonElement[] row, int index)
    {
        if (index >= row.Length) return null;
        var cell = row[index];
        return cell.ValueKind switch
        {
            JsonValueKind.Number => cell.TryGetInt32(out var i) ? i : null,
            JsonValueKind.String when int.TryParse(cell.GetString(), out var i) => i,
            _ => null,
        };
    }

    private static Guid? ParseNullableGuid(JsonElement[] row, int index)
    {
        if (index >= row.Length) return null;
        var cell = row[index];
        if (cell.ValueKind != JsonValueKind.String) return null;
        var s = cell.GetString();
        if (string.IsNullOrEmpty(s)) return null;
        return Guid.TryParse(s, out var g) && g != Guid.Empty ? g : null;
    }

    private static string NormalizeSeverity(string severity)
    {
        return severity.Trim().ToLowerInvariant() switch
        {
            "warning" => "warn",
            "critical" => "error",
            "" => "info",
            var value => value,
        };
    }

    /// <summary>
    /// Coerce a raw severity query-param into one of the four canonical
    /// log levels we filter on, or empty string for "no filter".
    /// Anything we don't recognise (incl. "all") falls through to no
    /// filter so callers get the unfiltered slice rather than zero rows.
    /// </summary>
    private static string NormaliseSeverityFilter(string? severity)
    {
        if (string.IsNullOrWhiteSpace(severity)) return string.Empty;
        return severity.Trim().ToLowerInvariant() switch
        {
            "debug" => "debug",
            "info" or "information" => "info",
            "warn" or "warning" => "warn",
            "error" or "critical" => "error",
            _ => string.Empty,
        };
    }

    private static double ComputeHitRate(long hits, long misses)
    {
        var total = hits + misses;
        if (total <= 0) return 0;
        return Math.Round((double)hits / total * 100, 2);
    }

    private static DateTime? ParseDateTimeNullable(JsonElement[] row, int index)
    {
        if (index >= row.Length) return null;
        return row[index].ValueKind == JsonValueKind.String
            && DateTime.TryParse(row[index].GetString(), out var dt)
                ? dt
                : null;
    }

    /// <summary>
    /// Reads a KQL <c>make_set()</c> / <c>make_list()</c> column, which
    /// App Insights returns either inline (<see cref="JsonValueKind.Array"/>)
    /// or as a JSON-encoded string. Returns an empty list on nulls or
    /// parse failures so callers never have to null-check.
    /// </summary>
    private static IReadOnlyList<string> GetStringArray(JsonElement[] row, int index)
    {
        if (index >= row.Length) return [];
        var cell = row[index];

        switch (cell.ValueKind)
        {
            case JsonValueKind.Array:
                return cell.EnumerateArray()
                    .Select(e => e.ValueKind == JsonValueKind.String
                        ? e.GetString() ?? string.Empty
                        : e.ToString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

            case JsonValueKind.String:
                var str = cell.GetString();
                if (string.IsNullOrWhiteSpace(str)) return [];
                try
                {
                    using var doc = JsonDocument.Parse(str);
                    if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];
                    return doc.RootElement.EnumerateArray()
                        .Select(e => e.ValueKind == JsonValueKind.String
                            ? e.GetString() ?? string.Empty
                            : e.ToString())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();
                }
                catch (JsonException)
                {
                    return [];
                }

            default:
                return [];
        }
    }

    private static IReadOnlyDictionary<string, string> ParseStringDictionary(JsonElement[] row, int index)
    {
        if (index >= row.Length) return new Dictionary<string, string>(StringComparer.Ordinal);
        var cell = row[index];
        if (cell.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        JsonElement root;
        JsonDocument? doc = null;
        try
        {
            if (cell.ValueKind == JsonValueKind.Object)
            {
                root = cell;
            }
            else if (cell.ValueKind == JsonValueKind.String)
            {
                var raw = cell.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                    return new Dictionary<string, string>(StringComparer.Ordinal);

                doc = JsonDocument.Parse(raw);
                root = doc.RootElement;
            }
            else
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            if (root.ValueKind != JsonValueKind.Object)
                return new Dictionary<string, string>(StringComparer.Ordinal);

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var prop in root.EnumerateObject())
            {
                var value = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                    JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                    _ => prop.Value.ToString(),
                };

                if (!string.IsNullOrWhiteSpace(value))
                {
                    result[prop.Name] = value;
                }
            }

            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
        finally
        {
            doc?.Dispose();
        }
    }

    // ── Error-group helpers ──────────────────────────────────────────
    //
    // The errors-tab list and the overview's top-errors list share the
    // same projection so they stay in lock-step. KQL is built once here
    // and fed into CachedQueryAsync with independent cache keys.

    private static string BuildErrorGroupsKql(string ago, string? operationId = null)
    {
        var operationFilter = string.IsNullOrWhiteSpace(operationId)
            ? string.Empty
            : $"| where operation_Id == \"{EscapeKql(operationId.Trim())}\"";

        return $$"""
        exceptions
        | where timestamp > {{ago}}
        {{operationFilter}}
        | extend firstMethod = tostring(details[0].parsedStack[0].method)
        | extend groupedMethod = iff(isempty(method), firstMethod, method)
        | summarize
            count=count(),
            lastSeen=max(timestamp),
            sampleOperationId=any(operation_Id),
            operations=make_set(operation_Name, 5),
            roles=make_set(cloud_RoleName, 3)
          by problemId, type, outerMessage, innermostMessage, groupedMethod
        | order by count desc
        | take 50
        """;
    }

    private static string EscapeKql(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal);

    /// <summary>
    /// Parses a row produced by <see cref="BuildErrorGroupsKql"/>. The
    /// column order is problemId, type, outerMessage, innermostMessage,
    /// method, count, lastSeen, sampleOperationId, operations, roles.
    /// </summary>
    private static ErrorGroup ParseErrorGroupRow(JsonElement[] row)
    {
        var operations = GetStringArray(row, 8);
        var roles = GetStringArray(row, 9);

        return new ErrorGroup(
            Type: GetString(row, 1),
            OuterMessage: GetString(row, 2),
            InnermostMessage: GetString(row, 3),
            Count: (long)ParseDouble(row, 5),
            LastSeen: ParseDateTime(row, 6),
            ProblemId: NullIfEmpty(GetString(row, 0)),
            Method: NullIfEmpty(GetString(row, 4)),
            SampleOperationId: NullIfEmpty(GetString(row, 7)),
            Operations: operations.Count > 0 ? operations : null,
            Roles: roles.Count > 0 ? roles : null);
    }

    /// <summary>
    /// Parses the <c>details[0].parsedStack</c> JSON array returned by
    /// <c>GetErrorDetailAsync</c>. The App Insights schema for each
    /// frame is <c>{ level, method, assembly, fileName, line }</c> with
    /// any field optional.
    /// </summary>
    private static IReadOnlyList<ErrorStackFrame> ParseStackJson(string stackJson)
    {
        if (string.IsNullOrWhiteSpace(stackJson) || stackJson == "null") return [];

        try
        {
            using var doc = JsonDocument.Parse(stackJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];

            var frames = new List<ErrorStackFrame>();
            foreach (var frame in doc.RootElement.EnumerateArray())
            {
                if (frame.ValueKind != JsonValueKind.Object) continue;

                int level = frame.TryGetProperty("level", out var lvl) && lvl.ValueKind == JsonValueKind.Number
                    ? lvl.GetInt32()
                    : frames.Count;
                string? method = frame.TryGetProperty("method", out var m) && m.ValueKind == JsonValueKind.String
                    ? m.GetString()
                    : null;
                string? assembly = frame.TryGetProperty("assembly", out var a) && a.ValueKind == JsonValueKind.String
                    ? a.GetString()
                    : null;
                string? fileName = frame.TryGetProperty("fileName", out var f) && f.ValueKind == JsonValueKind.String
                    ? f.GetString()
                    : null;
                int? line = frame.TryGetProperty("line", out var l) && l.ValueKind == JsonValueKind.Number
                    ? l.GetInt32()
                    : null;

                frames.Add(new ErrorStackFrame(level, method, assembly, fileName, line));
            }

            return frames;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Flattens the <c>customDimensions</c> bag into a string-keyed
    /// dictionary. Nested objects and arrays are JSON-stringified so the
    /// UI can display them inline without a recursive renderer.
    /// </summary>
    private static IReadOnlyDictionary<string, string> ParseCustomDimensionsJson(string json)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json) || json == "null") return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return result;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                result[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                    JsonValueKind.Null => string.Empty,
                    _ => prop.Value.ToString(),
                };
            }
        }
        catch (JsonException)
        {
            // Leave result empty — corrupted customDimensions shouldn't blank out the page.
        }

        return result;
    }

    // ── App Insights response model ──────────────────────────────────

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
}
