using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Aonik.Ai.Contracts.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Aonik.Ai.Services;

internal sealed class LangfuseAiTraceReader : IAiTraceReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly AiTraceExplorerOptions _options;

    public LangfuseAiTraceReader(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IOptions<AiTraceExplorerOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _options = options.Value;
    }

    public string ProviderName => "Langfuse";

    public Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(_configuration["Langfuse:PublicKey"])
                               && !string.IsNullOrWhiteSpace(_configuration["Langfuse:SecretKey"]));
    }

    public async Task<ListAiTraceObservationsResponse> ListObservationsAsync(
        ListAiTraceObservationsRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, request.Page.GetValueOrDefault(1));
        var pageSize = Math.Max(1, Math.Min(_options.MaxPageSize, request.PageSize.GetValueOrDefault(_options.DefaultPageSize)));
        var query = new Dictionary<string, string?>
        {
            ["page"] = page.ToString(),
            ["limit"] = pageSize.ToString(),
        };

        if (!string.IsNullOrWhiteSpace(request.Type)) query["type"] = request.Type.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(request.Name)) query["name"] = request.Name.Trim();
        if (!string.IsNullOrWhiteSpace(request.Environment)) query["environment"] = request.Environment.Trim();
        if (!string.IsNullOrWhiteSpace(request.Level)) query["level"] = request.Level.Trim().ToUpperInvariant();

        if (TryResolveRangeFloor(request.TimeRange, out var rangeFloor))
        {
            query["fromStartTime"] = rangeFloor.ToString("O");
        }

        var response = await CreateClient().GetFromJsonAsync<LangfuseObservationListResponse>(
            $"api/public/observations?{BuildQueryString(query)}",
            JsonOptions,
            cancellationToken);

        var items = (response?.Data ?? [])
            .Where(item => request.IsRootObservation is null || (item.ParentObservationId is null) == request.IsRootObservation.Value)
            .Where(item => string.IsNullOrWhiteSpace(request.TraceId)
                           || string.Equals(item.TraceId, request.TraceId.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(item => string.IsNullOrWhiteSpace(request.TraceName)
                           || (item.TraceName?.Contains(request.TraceName.Trim(), StringComparison.OrdinalIgnoreCase) ?? false)
                           || (item.TraceId?.Contains(request.TraceName.Trim(), StringComparison.OrdinalIgnoreCase) ?? false))
            .Select(MapObservation)
            .Where(item => string.IsNullOrWhiteSpace(request.AgentName)
                           || string.Equals(item.AgentName, request.AgentName.Trim(), StringComparison.OrdinalIgnoreCase)
                           || string.Equals(item.AgentId, request.AgentName.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new ListAiTraceObservationsResponse(
            items,
            response?.Meta?.TotalItems ?? items.Count,
            response?.Meta?.Page ?? page,
            response?.Meta?.Limit ?? pageSize,
            ProviderName);
    }

    private HttpClient CreateClient()
    {
        var baseUrl = _configuration["Langfuse:BaseUrl"] ?? "https://cloud.langfuse.com";
        var publicKey = _configuration["Langfuse:PublicKey"] ?? string.Empty;
        var secretKey = _configuration["Langfuse:SecretKey"] ?? string.Empty;
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{publicKey}:{secretKey}"));

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        return client;
    }

    private static AiTraceObservationResponse MapObservation(LangfuseObservation observation)
    {
        var metadata = ToJsonOrNull(observation.Metadata);
        var inputTokens = observation.UsageDetails?.Input ?? observation.Usage?.Input ?? observation.PromptTokens;
        var outputTokens = observation.UsageDetails?.Output ?? observation.Usage?.Output ?? observation.CompletionTokens;
        var totalTokens = observation.UsageDetails?.Total ?? observation.Usage?.Total ?? observation.TotalTokens;
        var agentId = ExtractMetadataString(observation.Metadata, "gen_ai.agent.id")
            ?? ExtractMetadataString(observation.Metadata, "aonik.agent.name");
        var agentName = ExtractMetadataString(observation.Metadata, "gen_ai.agent.name")
            ?? ExtractMetadataString(observation.Metadata, "aonik.agent.name");

        return new AiTraceObservationResponse
        {
            ObservationId = observation.Id ?? string.Empty,
            TraceId = observation.TraceId ?? string.Empty,
            ParentObservationId = observation.ParentObservationId,
            SpanId = observation.Id,
            ParentSpanId = observation.ParentObservationId,
            OperationId = observation.TraceId,
            AiRunId = ExtractAiRunId(observation.Metadata),
            StartTime = observation.StartTime,
            EndTime = observation.EndTime,
            Type = NormalizeType(observation.Type),
            Name = observation.Name ?? string.Empty,
            TraceName = observation.TraceName,
            Input = ToJsonOrNull(observation.Input),
            Output = ToJsonOrNull(observation.Output),
            Metadata = metadata,
            AgentId = agentId,
            AgentName = agentName,
            ServiceName = "langfuse",
            Level = observation.Level ?? "DEFAULT",
            LatencySeconds = observation.Latency,
            DurationMs = observation.Latency * 1000,
            CostUsd = observation.CostDetails?.Total ?? observation.CalculatedTotalCost ?? observation.TotalPrice,
            TimeToFirstTokenSeconds = observation.TimeToFirstToken,
            ProvidedModel = observation.Model,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = totalTokens,
            IsRootObservation = observation.ParentObservationId is null,
            Source = "Langfuse",
        };
    }

    private static Guid? ExtractAiRunId(JsonElement? metadata)
    {
        if (metadata is null || metadata.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (TryGetGuid(metadata.Value, "aonik.ai_run_id", out var direct)) return direct;
        if (metadata.Value.TryGetProperty("attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Object)
        {
            if (TryGetGuid(attrs, "aonik.ai_run_id", out var fromAttrs)) return fromAttrs;
            if (TryGetGuid(attrs, "AiRunId", out var legacy)) return legacy;
        }

        return null;
    }

    private static string? ExtractMetadataString(JsonElement? metadata, string propertyName)
    {
        if (metadata is null || metadata.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (TryGetString(metadata.Value, propertyName, out var direct)) return direct;
        if (metadata.Value.TryGetProperty("attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Object)
        {
            if (TryGetString(attrs, propertyName, out var fromAttrs)) return fromAttrs;
        }

        return null;
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out var prop)) return false;

        value = prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetGuid(JsonElement element, string propertyName, out Guid value)
    {
        value = default;
        return element.TryGetProperty(propertyName, out var prop)
               && prop.ValueKind == JsonValueKind.String
               && Guid.TryParse(prop.GetString(), out value);
    }

    private static string NormalizeType(string? type)
    {
        return string.IsNullOrWhiteSpace(type) ? "SPAN" : type.Trim().ToUpperInvariant();
    }

    private static string? ToJsonOrNull(JsonElement? element)
    {
        if (element is null || element.Value.ValueKind == JsonValueKind.Null || element.Value.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        return element.Value.GetRawText();
    }

    private static string BuildQueryString(Dictionary<string, string?> values)
    {
        return string.Join("&", values
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));
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

    private sealed record LangfuseObservationListResponse(
        IReadOnlyList<LangfuseObservation> Data,
        LangfuseMeta Meta);

    private sealed record LangfuseMeta(int Page, int Limit, int TotalItems, int TotalPages);

    private sealed record LangfuseUsage(int? Input, int? Output, int? Total, string? Unit);

    private sealed record LangfuseCost(decimal? Input, decimal? Output, decimal? Total);

    private sealed record LangfuseObservation
    {
        public string? Id { get; init; }
        public string? TraceId { get; init; }
        public string? ParentObservationId { get; init; }
        public string? Type { get; init; }
        public string? Name { get; init; }
        public string? TraceName { get; init; }
        public DateTime StartTime { get; init; }
        public DateTime? EndTime { get; init; }
        public string? Level { get; init; }
        public JsonElement? Input { get; init; }
        public JsonElement? Output { get; init; }
        public JsonElement? Metadata { get; init; }
        public string? Model { get; init; }
        public double? Latency { get; init; }
        public double? TimeToFirstToken { get; init; }
        public decimal? CalculatedTotalCost { get; init; }
        public decimal? TotalPrice { get; init; }
        public int? PromptTokens { get; init; }
        public int? CompletionTokens { get; init; }
        public int? TotalTokens { get; init; }
        public LangfuseUsage? Usage { get; init; }
        public LangfuseUsage? UsageDetails { get; init; }
        public LangfuseCost? CostDetails { get; init; }
    }
}
