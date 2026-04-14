using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aonik.Platform.Contracts.Api.Observability;
using Aonik.Platform.Contracts.Services.Observability;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Settings;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Infrastructure.Observability;

public class AppInsightsQueryService : IObservabilityService
{
    private readonly ISettingProvider _settingProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFusionCache _cache;
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
        ILogger<AppInsightsQueryService> logger)
    {
        _settingProvider = settingProvider;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
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

        await Task.WhenAll(requestTimeSeriesTask, errorTimeSeriesTask, latencyPercentilesTask, latencyTimeSeriesTask);

        var requestRows = requestTimeSeriesTask.Result;
        var errorRows = errorTimeSeriesTask.Result;
        var latencyPercentileRows = latencyPercentilesTask.Result;
        var latencyRows = latencyTimeSeriesTask.Result;

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

        // Top errors (separate query)
        var topErrorRows = await CachedQueryAsync(
            $"observability:topErrors:{timeRange}", appId, apiKey,
            $"exceptions | where timestamp > {range.Ago} | summarize count=count(), lastSeen=max(timestamp) by type, outerMessage, innermostMessage | order by count desc | take 50",
            cancellationToken);

        var topErrors = topErrorRows.Select(r => new ErrorGroup(
            GetString(r, 0), GetString(r, 1), GetString(r, 2),
            (long)ParseDouble(r, 3), ParseDateTime(r, 4))).ToList();

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
        string timeRange, CancellationToken cancellationToken = default)
    {
        var (appId, apiKey) = await GetCredentialsAsync(cancellationToken);
        if (appId is null || apiKey is null)
            return new ErrorsResponse(false, []);

        var range = ParseTimeRange(timeRange);

        var rows = await CachedQueryAsync(
            $"observability:errors:{timeRange}", appId, apiKey,
            $"exceptions | where timestamp > {range.Ago} | summarize count=count(), lastSeen=max(timestamp) by type, outerMessage, innermostMessage | order by count desc | take 50",
            cancellationToken);

        var errors = rows.Select(r => new ErrorGroup(
            GetString(r, 0), GetString(r, 1), GetString(r, 2),
            (long)ParseDouble(r, 3), ParseDateTime(r, 4))).ToList();

        return new ErrorsResponse(true, errors);
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

        var timeSeriesTask = CachedQueryAsync(
            $"observability:aiTimeSeries:{timeRange}", appId, apiKey,
            $"dependencies | where timestamp > {range.Ago} | where type == \"Azure OpenAI\" or name contains \"openai\" | summarize count() by bin(timestamp, {range.Bin}) | order by timestamp asc",
            cancellationToken);

        var byAgentTask = CachedQueryAsync(
            $"observability:aiByAgent:{timeRange}", appId, apiKey,
            $"dependencies | where timestamp > {range.Ago} | where type == \"Azure OpenAI\" or name contains \"openai\" | extend agentName = tostring(customDimensions[\"gen_ai.operation.name\"]) | summarize calls=count(), avgDuration=avg(duration), totalTokens=sumif(toint(customDimensions[\"gen_ai.usage.output_tokens\"]) + toint(customDimensions[\"gen_ai.usage.input_tokens\"]), isnotempty(customDimensions[\"gen_ai.usage.output_tokens\"])) by agentName | order by calls desc",
            cancellationToken);

        await Task.WhenAll(timeSeriesTask, byAgentTask);

        var timeSeriesRows = timeSeriesTask.Result;
        var byAgentRows = byAgentTask.Result;

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

    public async Task<AiPerformanceResponse> GetAiPerformanceAsync(
        string timeRange, CancellationToken cancellationToken = default)
    {
        var (appId, apiKey) = await GetCredentialsAsync(cancellationToken);
        if (appId is null || apiKey is null)
            return new AiPerformanceResponse(false, null, null, null, [], null, [], [], [], [], []);

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

        await Task.WhenAll(latencyDistTask, ttftDistTask, tokenUsageTask,
            byAgentTask, byUseCaseTask, byModelTask,
            clientServerTask, latencyTsTask, ttftTsTask, tokenTsTask);

        // ── Parse latency distribution ──────────────────────────────────

        var latencyDistRows = latencyDistTask.Result;
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

        var ttftDistRows = ttftDistTask.Result;
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

        var tokenRows = tokenUsageTask.Result;
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

        var agentRows = byAgentTask.Result;
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

        var useCaseRows = byUseCaseTask.Result;
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

        var modelRows = byModelTask.Result;
        var byModel = modelRows.Select(r => new AiModelPerformance(
            GetString(r, 0),
            (long)ParseDouble(r, 1),
            Math.Round(ParseDouble(r, 2), 2),
            Math.Round(ParseDouble(r, 3), 2),
            (long)ParseDouble(r, 4),
            (long)ParseDouble(r, 5),
            Math.Round(ParseDouble(r, 6), 4))).ToList();

        // ── Parse client vs server comparison ───────────────────────────

        var csRows = clientServerTask.Result;
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

        var latencyTs = latencyTsTask.Result.Select(r => new TimeSeriesPoint(
            ParseDateTime(r, 0), Math.Round(ParseDouble(r, 1), 2))).ToList();

        var ttftTs = ttftTsTask.Result.Select(r => new TimeSeriesPoint(
            ParseDateTime(r, 0), Math.Round(ParseDouble(r, 1), 2))).ToList();

        var tokenTs = tokenTsTask.Result.Select(r => new TimeSeriesPoint(
            ParseDateTime(r, 0), ParseDouble(r, 1))).ToList();

        return new AiPerformanceResponse(true, latency, ttft, tokenUsage,
            byAgent, clientServer, latencyTs, ttftTs, tokenTs, byUseCase, byModel);
    }

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
        return row[index].GetString() ?? string.Empty;
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
