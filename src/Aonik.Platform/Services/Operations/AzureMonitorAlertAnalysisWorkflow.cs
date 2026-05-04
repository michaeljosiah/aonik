using System.Text.Json;
using Aonik.Platform.Entities.Operations;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aonik.Platform.Services.Operations;

internal interface IAlertAnalysisWorkflow
{
    Task<AlertAnalysisResult> AnalyzeAsync(AzureMonitorAlertEvent alertEvent, CancellationToken cancellationToken = default);
}

internal sealed record AlertAnalysisResult(
    Guid? AiRunId,
    string Summary,
    string LikelyCause,
    string Impact,
    string AffectedComponent,
    IReadOnlyList<string> RecommendedActions,
    string Confidence)
{
    public string ToJson()
    {
        return JsonSerializer.Serialize(new
        {
            Summary,
            LikelyCause,
            Impact,
            AffectedComponent,
            RecommendedActions,
            Confidence,
        });
    }
}

internal sealed class AzureMonitorAlertAnalysisWorkflow : IAlertAnalysisWorkflow
{
    private const string UseCase = "platform_alert_analysis";
    private const string PromptName = "platform_alert_analysis";
    private const string DefaultModelId = "gpt-5-mini";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IAiTaskProfileResolver _profileResolver;
    private readonly IChatClient _chatClient;
    private readonly IAiRunWriter _aiRunWriter;
    private readonly ILogger<AzureMonitorAlertAnalysisWorkflow> _logger;

    public AzureMonitorAlertAnalysisWorkflow(
        IAiTaskProfileResolver profileResolver,
        IChatClient chatClient,
        IAiRunWriter aiRunWriter,
        ILogger<AzureMonitorAlertAnalysisWorkflow> logger)
    {
        _profileResolver = profileResolver;
        _chatClient = chatClient;
        _aiRunWriter = aiRunWriter;
        _logger = logger;
    }

    public async Task<AlertAnalysisResult> AnalyzeAsync(AzureMonitorAlertEvent alertEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alertEvent);

        var inputJson = JsonSerializer.Serialize(new
        {
            alertEvent.Id,
            alertEvent.ExternalAlertId,
            alertEvent.AlertRuleName,
            alertEvent.AlertRuleId,
            alertEvent.MonitorCondition,
            alertEvent.Severity,
            alertEvent.SignalType,
            alertEvent.MonitoringService,
            alertEvent.NormalizedType,
            alertEvent.CorrelationKey,
            alertEvent.ReceivedAtUtc,
            alertEvent.FiredAtUtc,
            alertEvent.ResolvedAtUtc,
            ResourceIds = DeserializeStringList(alertEvent.ResourceIdsJson),
            CustomProperties = DeserializeDictionary(alertEvent.CustomPropertiesJson),
            Essentials = DeserializeJson(alertEvent.EssentialsJson),
        }, JsonOptions);

        var aiRunId = await _aiRunWriter.StartRunAsync(UseCase, inputJson, cancellationToken);

        try
        {
            var profile = await _profileResolver.ResolveAsync(UseCase, PromptName, DefaultModelId, cancellationToken);
            var userPrompt = (profile.UserPromptTemplate ?? "{{ALERT_JSON}}")
                .Replace("{{ALERT_JSON}}", inputJson, StringComparison.Ordinal);

            var messages = new List<ChatMessage>();
            if (!string.IsNullOrWhiteSpace(profile.SystemPrompt))
            {
                messages.Add(new ChatMessage(ChatRole.System, profile.SystemPrompt));
            }

            messages.Add(new ChatMessage(ChatRole.User, userPrompt));

            var options = new ChatOptions
            {
                ModelId = profile.ModelId,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    [AiTelemetry.UseCaseAttribute] = UseCase,
                },
            };
            var response = await _chatClient.GetResponseAsync(messages, options, cancellationToken);
            var parsed = TryParse(response.Text);
            var result = parsed ?? BuildFallback(alertEvent);

            await _aiRunWriter.MarkRunCompletedAsync(aiRunId, $"platform-alert:{alertEvent.Id}", cancellationToken);
            return result with { AiRunId = aiRunId };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Alert analysis workflow failed for alert {AlertId}; using deterministic fallback.", alertEvent.Id);
            await TryMarkFailedAsync(aiRunId, ex.Message);
            return BuildFallback(alertEvent) with { AiRunId = aiRunId };
        }
    }

    private AlertAnalysisResult? TryParse(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;

            return new AlertAnalysisResult(
                AiRunId: null,
                Summary: ReadString(root, "summary"),
                LikelyCause: ReadString(root, "likelyCause"),
                Impact: ReadString(root, "impact"),
                AffectedComponent: ReadString(root, "affectedComponent"),
                RecommendedActions: ReadStringArray(root, "recommendedActions"),
                Confidence: ReadString(root, "confidence"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static AlertAnalysisResult BuildFallback(AzureMonitorAlertEvent alertEvent)
    {
        var resources = DeserializeStringList(alertEvent.ResourceIdsJson);
        var primaryResource = resources.FirstOrDefault() ?? "the monitored platform resource";
        var resolved = string.Equals(alertEvent.MonitorCondition, AzureMonitorAlertConditions.Resolved, StringComparison.OrdinalIgnoreCase);
        var summary = resolved
            ? $"{alertEvent.AlertRuleName} has recovered and the platform resource is reporting healthy again."
            : $"{alertEvent.AlertRuleName} is active and needs operator review for {primaryResource}.";
        var likelyCause = resolved
            ? "The monitored condition has returned to its healthy threshold window."
            : "A platform threshold or log-based monitor detected an unhealthy runtime, dependency, or resource condition.";
        var impact = resolved
            ? "The platform condition is no longer actively failing, but recent logs and deployments should still be reviewed for regression risk."
            : "Platform reliability, security posture, or operator visibility may be degraded until the underlying issue is mitigated.";
        var affectedComponent = primaryResource;
        var recommendedActions = resolved
            ? new[]
            {
                "Confirm the service remains healthy for the next evaluation window.",
                "Review recent deployments, configuration changes, and incident notes.",
                "Close or downgrade any active incident if no follow-up work remains."
            }
            : new[]
            {
                "Open the alert detail page and review the affected resource IDs and raw alert context.",
                "Inspect recent application exceptions, dependencies, and container logs for the impacted service.",
                "Check for recent deployments, secret changes, or infrastructure updates that could explain the alert."
            };

        return new AlertAnalysisResult(
            AiRunId: null,
            Summary: summary,
            LikelyCause: likelyCause,
            Impact: impact,
            AffectedComponent: affectedComponent,
            RecommendedActions: recommendedActions,
            Confidence: "Medium");
    }

    private async Task TryMarkFailedAsync(Guid aiRunId, string failureReason)
    {
        try
        {
            await _aiRunWriter.MarkRunFailedAsync(aiRunId, failureReason, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to mark platform alert AiRun {AiRunId} as failed.", aiRunId);
        }
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        return property.GetString()?.Trim() ?? string.Empty;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return property.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString()?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToList();
    }

    private static IReadOnlyList<string> DeserializeStringList(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyDictionary<string, string> DeserializeDictionary(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private static JsonElement DeserializeJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            using var document = JsonDocument.Parse("{}");
            return document.RootElement.Clone();
        }
    }
}
