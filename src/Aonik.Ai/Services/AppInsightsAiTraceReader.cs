using System.Net.Http.Json;
using System.Text.Json;
using Aonik.Ai.Contracts.Models;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Settings;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Ai.Services;

internal sealed class AppInsightsAiTraceReader : IAiTraceReader
{
    private readonly ISettingProvider _settingProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFusionCache _cache;
    private readonly AiTraceExplorerOptions _options;

    public AppInsightsAiTraceReader(
        ISettingProvider settingProvider,
        IHttpClientFactory httpClientFactory,
        IFusionCache cache,
        IOptions<AiTraceExplorerOptions> options)
    {
        _settingProvider = settingProvider;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _options = options.Value;
    }

    public string ProviderName => "AppInsights";

    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
    {
        var (appId, apiKey) = await GetCredentialsAsync(cancellationToken);
        return appId is not null && apiKey is not null;
    }

    public async Task<ListAiTraceObservationsResponse> ListObservationsAsync(
        ListAiTraceObservationsRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, request.Page.GetValueOrDefault(1));
        var pageSize = Math.Max(1, Math.Min(_options.MaxPageSize, request.PageSize.GetValueOrDefault(_options.DefaultPageSize)));
        var (appId, apiKey) = await GetCredentialsAsync(cancellationToken);
        if (appId is null || apiKey is null)
        {
            return new ListAiTraceObservationsResponse([], 0, page, pageSize, ProviderName);
        }

        var rows = await _cache.GetOrSetAsync(
            $"ai-trace-observations:appinsights:{page}:{pageSize}:{request.Type}:{request.Name}:{request.TraceName}:{request.Environment}:{request.Level}:{request.IsRootObservation}:{request.TimeRange}",
            async ct => await ExecuteQueryAsync(appId, apiKey, BuildKql(request, page, pageSize), ct),
            new FusionCacheEntryOptions(TimeSpan.FromSeconds(60)),
            cancellationToken) ?? [];

        var items = rows.Select(ParseRow).Where(item => item is not null).Select(item => item!).ToList();

        return new ListAiTraceObservationsResponse(items, items.Count, page, pageSize, ProviderName);
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

        return string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(apiKey)
            ? (null, null)
            : (appId, apiKey);
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

        var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var body = await response.Content.ReadFromJsonAsync<AppInsightsQueryResponse>(cancellationToken: cancellationToken);
        return body?.Tables.FirstOrDefault()?.Rows.Select(row => row.Select(cell => cell).ToArray()).ToList() ?? [];
    }

    private static AiTraceObservationResponse? ParseRow(JsonElement[] row)
    {
        if (row.Length < 19)
        {
            return null;
        }

        Guid? aiRunId = Guid.TryParse(GetString(row, 3), out var parsedRunId) ? parsedRunId : null;

        return new AiTraceObservationResponse
        {
            ObservationId = GetString(row, 0),
            TraceId = GetString(row, 1),
            ParentObservationId = NullIfWhiteSpace(GetString(row, 2)),
            AiRunId = aiRunId,
            StartTime = ParseDateTime(row, 4),
            EndTime = ParseDateTimeNullable(row, 5),
            Type = NullIfWhiteSpace(GetString(row, 6)) ?? "GENERATION",
            Name = NullIfWhiteSpace(GetString(row, 7)) ?? "chat",
            TraceName = NullIfWhiteSpace(GetString(row, 8)),
            Input = NullIfWhiteSpace(GetString(row, 9)),
            Output = NullIfWhiteSpace(GetString(row, 10)),
            Metadata = NullIfWhiteSpace(GetString(row, 11)),
            Level = NullIfWhiteSpace(GetString(row, 12)) ?? "DEFAULT",
            LatencySeconds = ParseDoubleNullable(row, 13),
            CostUsd = ParseDecimalNullable(row, 14),
            TimeToFirstTokenSeconds = ParseDoubleNullable(row, 15),
            ProvidedModel = NullIfWhiteSpace(GetString(row, 16)),
            InputTokens = ParseIntNullable(row, 17),
            OutputTokens = ParseIntNullable(row, 18),
            TotalTokens = row.Length > 19 ? ParseIntNullable(row, 19) : null,
            IsRootObservation = string.IsNullOrWhiteSpace(GetString(row, 2)),
            Source = "AppInsights",
        };
    }

    private static string BuildKql(ListAiTraceObservationsRequest request, int page, int pageSize)
    {
        var ago = request.TimeRange switch
        {
            "1h" => "ago(1h)",
            "7d" => "ago(7d)",
            "30d" => "ago(30d)",
            _ => "ago(24h)",
        };

        var filters = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Type)) filters.Add($"type == \"{EscapeKql(request.Type.Trim().ToUpperInvariant())}\"");
        if (!string.IsNullOrWhiteSpace(request.Name)) filters.Add($"name contains \"{EscapeKql(request.Name.Trim())}\"");
        if (!string.IsNullOrWhiteSpace(request.TraceName)) filters.Add($"traceName contains \"{EscapeKql(request.TraceName.Trim())}\"");
        if (!string.IsNullOrWhiteSpace(request.Level)) filters.Add($"level == \"{EscapeKql(request.Level.Trim().ToUpperInvariant())}\"");
        if (request.IsRootObservation is { } isRoot) filters.Add(isRoot ? "isempty(parentObservationId)" : "isnotempty(parentObservationId)");

        var whereFilters = filters.Count == 0 ? string.Empty : "| where " + string.Join(" and ", filters);
        var skip = (page - 1) * pageSize;

        return $"""
        traces
        | where timestamp > {ago}
        | where message startswith "AiTraceObservation" or message startswith "AiCallCompleted"
        | extend observationId = tostring(customDimensions["ObservationId"]),
                 traceId = tostring(customDimensions["TraceId"]),
                 parentObservationId = tostring(customDimensions["ParentObservationId"]),
                 aiRunId = tostring(customDimensions["AiRunId"]),
                 type = tostring(customDimensions["ObservationType"]),
                 name = tostring(customDimensions["Name"]),
                 traceName = tostring(customDimensions["TraceName"]),
                 input = tostring(customDimensions["InputJson"]),
                 output = tostring(customDimensions["OutputJson"]),
                 metadata = tostring(customDimensions["MetadataJson"]),
                 level = tostring(customDimensions["Level"]),
                 latencySeconds = todouble(customDimensions["LatencySeconds"]),
                 costUsd = todouble(customDimensions["CostUsd"]),
                 ttftSeconds = todouble(customDimensions["TimeToFirstTokenSeconds"]),
                 providedModel = tostring(customDimensions["ProvidedModel"]),
                 inputTokens = toint(customDimensions["InputTokens"]),
                 outputTokens = toint(customDimensions["OutputTokens"]),
                 totalTokens = toint(customDimensions["TotalTokens"])
        | extend type = iff(isempty(type), "GENERATION", type),
                 name = iff(isempty(name), tostring(customDimensions["Operation"]), name),
                 traceName = iff(isempty(traceName), tostring(customDimensions["UseCase"]), traceName),
                 latencySeconds = iff(isnan(latencySeconds), todouble(customDimensions["LatencyMs"]) / 1000.0, latencySeconds),
                 costUsd = iff(isnan(costUsd), todouble(customDimensions["EstimatedCostUsd"]), costUsd),
                 ttftSeconds = iff(isnan(ttftSeconds), todouble(customDimensions["TtftMs"]) / 1000.0, ttftSeconds),
                 providedModel = iff(isempty(providedModel), tostring(customDimensions["ActualModel"]), providedModel),
                 inputTokens = iff(isnull(inputTokens), toint(customDimensions["InputTokens"]), inputTokens),
                 outputTokens = iff(isnull(outputTokens), toint(customDimensions["OutputTokens"]), outputTokens),
                 totalTokens = iff(isnull(totalTokens), toint(customDimensions["TotalTokens"]), totalTokens)
        {whereFilters}
        | sort by timestamp desc
        | serialize rn = row_number()
        | where rn > {skip} and rn <= {skip + pageSize}
        | project observationId, traceId, parentObservationId, aiRunId, timestamp, endTime=datetime(null), type, name, traceName, input, output, metadata, level, latencySeconds, costUsd, ttftSeconds, providedModel, inputTokens, outputTokens, totalTokens
        """;
    }

    private static string EscapeKql(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string GetString(JsonElement[] row, int index)
    {
        if (index >= row.Length) return string.Empty;
        return row[index].ValueKind switch
        {
            JsonValueKind.String => row[index].GetString() ?? string.Empty,
            JsonValueKind.Null => string.Empty,
            _ => row[index].ToString(),
        };
    }

    private static DateTime ParseDateTime(JsonElement[] row, int index) =>
        DateTime.TryParse(GetString(row, index), out var value) ? value : DateTime.MinValue;

    private static DateTime? ParseDateTimeNullable(JsonElement[] row, int index) =>
        DateTime.TryParse(GetString(row, index), out var value) ? value : null;

    private static double? ParseDoubleNullable(JsonElement[] row, int index) =>
        double.TryParse(GetString(row, index), out var value) && double.IsFinite(value) ? value : null;

    private static decimal? ParseDecimalNullable(JsonElement[] row, int index) =>
        decimal.TryParse(GetString(row, index), out var value) ? value : null;

    private static int? ParseIntNullable(JsonElement[] row, int index) =>
        int.TryParse(GetString(row, index), out var value) ? value : null;

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed class AppInsightsQueryResponse
    {
        public IReadOnlyList<AppInsightsTable> Tables { get; set; } = [];
    }

    private sealed class AppInsightsTable
    {
        public IReadOnlyList<IReadOnlyList<JsonElement>> Rows { get; set; } = [];
    }
}
