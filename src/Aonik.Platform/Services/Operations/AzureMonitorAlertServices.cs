using System.Text.Json;
using Aonik.Platform.Contracts.Api.Operations;
using Aonik.Platform.Contracts.Services.Notifications;
using Aonik.Platform.Contracts.Services.Operations;
using Aonik.Platform.Entities.Operations;
using Aonik.Platform.Notifications;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Platform.Services.Operations;

/// <summary>
/// Resolves the set of platform-admin user IDs that should receive an
/// alert. Concrete class injected directly — the
/// <c>IAlertAudienceResolver</c> interface that previously fronted this
/// class was a single-impl wrapper with no test double or alternate
/// implementation. Deleted by the 2026-05-05 single-impl audit.
/// </summary>
internal sealed class PlatformAdminAlertAudienceResolver
{
    private readonly PlatformDbContext _dbContext;

    public PlatformAdminAlertAudienceResolver(PlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<Guid>> ResolveUserIdsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserRoles
            .AcrossTenants()
            .Join(
                _dbContext.Roles.AcrossTenants(),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, RoleName = role.Name, RoleTenantId = role.TenantId })
            .Join(
                _dbContext.Users.AcrossTenants(),
                item => item.UserId,
                user => user.Id,
                (item, user) => new { item.UserId, item.RoleName, item.RoleTenantId, user.Status })
            .Where(item => item.RoleTenantId == Guid.Empty
                && item.RoleName == "PlatformAdmin"
                && item.Status == "Active")
            .Select(item => item.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}

internal sealed class AlertIngestionService : IAlertIngestionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly PlatformDbContext _dbContext;
    private readonly IAlertProcessingQueue _processingQueue;
    private readonly IClock _clock;
    private readonly ILogger<AlertIngestionService> _logger;

    public AlertIngestionService(
        PlatformDbContext dbContext,
        IAlertProcessingQueue processingQueue,
        IClock clock,
        ILogger<AlertIngestionService> logger)
    {
        _dbContext = dbContext;
        _processingQueue = processingQueue;
        _clock = clock;
        _logger = logger;
    }

    public async Task<AlertWebhookAcceptedResponse> IngestAzureMonitorAlertAsync(
        AzureMonitorAlertWebhookRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var essentials = request.Data!.Essentials!;
        var existing = await _dbContext.Set<AzureMonitorAlertEvent>()
            .AcrossTenants()
            .FirstOrDefaultAsync(x => x.ExternalAlertId == essentials.AlertId!, cancellationToken);

        if (existing is not null)
        {
            return new AlertWebhookAcceptedResponse(existing.Id, existing.Status);
        }

        var resourceIds = essentials.AlertTargetIDs?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList() ?? [];
        var customProperties = request.Data.CustomProperties ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var normalizedType = NormalizeType(essentials, customProperties);
        var alertEvent = new AzureMonitorAlertEvent
        {
            TenantId = Guid.Empty,
            Provider = AzureMonitorAlertProviders.AzureMonitor,
            ExternalAlertId = essentials.AlertId!.Trim(),
            AlertRuleName = NormalizeRequired(essentials.AlertRule, nameof(essentials.AlertRule)),
            AlertRuleId = NormalizeOptional(essentials.OriginAlertId) ?? NormalizeRequired(essentials.AlertId, nameof(essentials.AlertId)),
            MonitorCondition = NormalizeRequired(essentials.MonitorCondition, nameof(essentials.MonitorCondition)),
            Severity = NormalizeRequired(essentials.Severity, nameof(essentials.Severity)),
            SignalType = NormalizeRequired(essentials.SignalType, nameof(essentials.SignalType)),
            MonitoringService = NormalizeOptional(essentials.MonitoringService) ?? "AzureMonitor",
            NormalizedType = normalizedType,
            CorrelationKey = BuildCorrelationKey(essentials, normalizedType, resourceIds, customProperties),
            Status = AzureMonitorAlertStatuses.Received,
            ResourceIdsJson = JsonSerializer.Serialize(resourceIds, JsonOptions),
            EssentialsJson = JsonSerializer.Serialize(essentials, JsonOptions),
            AlertContextJson = JsonSerializer.Serialize(request.Data.AlertContext, JsonOptions),
            CustomPropertiesJson = JsonSerializer.Serialize(customProperties, JsonOptions),
            AnalysisSummary = string.Empty,
            AnalysisJson = "{}",
            ReceivedAtUtc = _clock.UtcNow,
            FiredAtUtc = ParseUtc(essentials.FiredDateTime),
            ResolvedAtUtc = ParseUtc(essentials.ResolvedDateTime),
        };

        _dbContext.Set<AzureMonitorAlertEvent>().Add(alertEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _processingQueue.EnqueueAsync(alertEvent.Id, cancellationToken);

        _logger.LogInformation(
            "Accepted Azure Monitor alert {AlertId} ({RuleName}, {Condition}) as alert event {InternalAlertId}.",
            alertEvent.ExternalAlertId,
            alertEvent.AlertRuleName,
            alertEvent.MonitorCondition,
            alertEvent.Id);

        return new AlertWebhookAcceptedResponse(alertEvent.Id, alertEvent.Status);
    }

    private static void ValidateRequest(AzureMonitorAlertWebhookRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(request.SchemaId, "azureMonitorCommonAlertSchema", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only azureMonitorCommonAlertSchema payloads are supported.", nameof(request));
        }

        if (request.Data?.Essentials is null)
        {
            throw new ArgumentException("Azure Monitor alert payload is missing essentials data.", nameof(request));
        }

        _ = NormalizeRequired(request.Data.Essentials.AlertId, nameof(request.Data.Essentials.AlertId));
        _ = NormalizeRequired(request.Data.Essentials.AlertRule, nameof(request.Data.Essentials.AlertRule));
        _ = NormalizeRequired(request.Data.Essentials.Severity, nameof(request.Data.Essentials.Severity));
        _ = NormalizeRequired(request.Data.Essentials.SignalType, nameof(request.Data.Essentials.SignalType));
        _ = NormalizeRequired(request.Data.Essentials.MonitorCondition, nameof(request.Data.Essentials.MonitorCondition));
    }

    private static string NormalizeType(AzureMonitorAlertEssentials essentials, IReadOnlyDictionary<string, string> customProperties)
    {
        var category = GetCustomProperty(customProperties, "alertCategory")
            ?? InferCategoryFromRule(essentials.AlertRule);

        var resolved = string.Equals(essentials.MonitorCondition, AzureMonitorAlertConditions.Resolved, StringComparison.OrdinalIgnoreCase);

        return category switch
        {
            "security" => resolved ? AzureMonitorAlertTypes.PlatformSecurityResolved : AzureMonitorAlertTypes.PlatformSecurityAlert,
            "operations" => resolved ? AzureMonitorAlertTypes.PlatformOperationsResolved : AzureMonitorAlertTypes.PlatformOperationsAlert,
            "availability" => resolved ? AzureMonitorAlertTypes.PlatformAvailabilityResolved : AzureMonitorAlertTypes.PlatformAvailabilityAlert,
            _ => resolved ? AzureMonitorAlertTypes.PlatformPerformanceResolved : AzureMonitorAlertTypes.PlatformPerformanceAlert,
        };
    }

    private static string BuildCorrelationKey(
        AzureMonitorAlertEssentials essentials,
        string normalizedType,
        IReadOnlyList<string> resourceIds,
        IReadOnlyDictionary<string, string> customProperties)
    {
        var ruleId = NormalizeOptional(essentials.OriginAlertId) ?? NormalizeRequired(essentials.AlertRule, nameof(essentials.AlertRule));
        var primaryResource = resourceIds.FirstOrDefault() ?? "global";
        var environmentName = GetCustomProperty(customProperties, "environmentName") ?? "unknown";
        return $"{normalizedType}|{environmentName}|{ruleId}|{primaryResource}";
    }

    private static string InferCategoryFromRule(string? alertRule)
    {
        var normalized = alertRule?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Contains("security", StringComparison.Ordinal)
            || normalized.Contains("keyvault", StringComparison.Ordinal)
            || normalized.Contains("secret", StringComparison.Ordinal))
        {
            return "security";
        }

        if (normalized.Contains("job", StringComparison.Ordinal)
            || normalized.Contains("worker", StringComparison.Ordinal)
            || normalized.Contains("queue", StringComparison.Ordinal))
        {
            return "operations";
        }

        if (normalized.Contains("availability", StringComparison.Ordinal)
            || normalized.Contains("unavailable", StringComparison.Ordinal)
            || normalized.Contains("restart", StringComparison.Ordinal))
        {
            return "availability";
        }

        return "performance";
    }

    private static string? GetCustomProperty(IReadOnlyDictionary<string, string> customProperties, string name)
    {
        foreach (var property in customProperties)
        {
            if (string.Equals(property.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeOptional(property.Value);
            }
        }

        return null;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeRequired(string? value, string? parameterName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (parameterName is null)
            {
                return string.Empty;
            }

            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        return value.Trim();
    }

    private static DateTime? ParseUtc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParse(value, out var parsed)
            ? DateTime.SpecifyKind(parsed.ToUniversalTime(), DateTimeKind.Utc)
            : null;
    }
}

internal sealed class AlertAdminService : IAlertAdminService
{
    private readonly PlatformDbContext _dbContext;

    public AlertAdminService(PlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AlertListResponse> ListAlertsAsync(int take = 50, CancellationToken cancellationToken = default)
    {
        var safeTake = Math.Clamp(take, 1, 100);
        var alertRows = await _dbContext.Set<AzureMonitorAlertEvent>()
            .AsNoTracking()
            .Where(x => x.TenantId == Guid.Empty)
            .OrderByDescending(x => x.ReceivedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(safeTake)
            .ToListAsync(cancellationToken);
        var alerts = alertRows.Select(MapSummary).ToList();

        return new AlertListResponse(alerts);
    }

    public async Task<AlertDetailResponse?> GetAlertAsync(Guid alertId, CancellationToken cancellationToken = default)
    {
        var alertEvent = await _dbContext.Set<AzureMonitorAlertEvent>()
            .AsNoTracking()
            .Where(x => x.Id == alertId && x.TenantId == Guid.Empty)
            .FirstOrDefaultAsync(cancellationToken);

        return alertEvent is null ? null : MapDetail(alertEvent);
    }

    private static AlertSummaryResponse MapSummary(AzureMonitorAlertEvent alertEvent)
        => new(
            alertEvent.Id,
            alertEvent.AlertRuleName,
            alertEvent.MonitorCondition,
            alertEvent.Severity,
            alertEvent.SignalType,
            alertEvent.NormalizedType,
            alertEvent.Status,
            alertEvent.AnalysisSummary,
            alertEvent.ReceivedAtUtc,
            alertEvent.FiredAtUtc,
            alertEvent.ResolvedAtUtc,
            DeserializeStringList(alertEvent.ResourceIdsJson));

    private static AlertDetailResponse MapDetail(AzureMonitorAlertEvent alertEvent)
    {
        var essentials = DeserializeEssentials(alertEvent.EssentialsJson);

        return new AlertDetailResponse(
            alertEvent.Id,
            alertEvent.Provider,
            alertEvent.ExternalAlertId,
            alertEvent.AlertRuleName,
            alertEvent.AlertRuleId,
            alertEvent.MonitorCondition,
            alertEvent.Severity,
            alertEvent.SignalType,
            alertEvent.MonitoringService,
            alertEvent.NormalizedType,
            alertEvent.Status,
            alertEvent.CorrelationKey,
            alertEvent.ReceivedAtUtc,
            alertEvent.FiredAtUtc,
            alertEvent.ResolvedAtUtc,
            alertEvent.ProcessedAtUtc,
            alertEvent.AiRunId,
            essentials.Description ?? string.Empty,
            essentials.InvestigationLink ?? string.Empty,
            DeserializeStringList(alertEvent.ResourceIdsJson),
            DeserializeDictionary(alertEvent.CustomPropertiesJson),
            DeserializeAnalysis(alertEvent.AnalysisJson, alertEvent.AnalysisSummary),
            alertEvent.EssentialsJson,
            alertEvent.AlertContextJson);
    }

    private static AlertAnalysisResponse? DeserializeAnalysis(string json, string summary)
    {
        if (string.IsNullOrWhiteSpace(summary) && string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            var root = document.RootElement;
            return new AlertAnalysisResponse(
                string.IsNullOrWhiteSpace(summary) ? ReadString(root, "Summary") : summary,
                ReadString(root, "LikelyCause"),
                ReadString(root, "Impact"),
                ReadString(root, "AffectedComponent"),
                ReadStringArray(root, "RecommendedActions"),
                ReadString(root, "Confidence"));
        }
        catch (JsonException)
        {
            return new AlertAnalysisResponse(summary, string.Empty, string.Empty, string.Empty, [], string.Empty);
        }
    }

    private static AzureMonitorAlertEssentials DeserializeEssentials(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<AzureMonitorAlertEssentials>(json) ?? new AzureMonitorAlertEssentials(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
        }
        catch (JsonException)
        {
            return new AzureMonitorAlertEssentials(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
        }
    }

    private static Dictionary<string, string> DeserializeDictionary(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<string> DeserializeStringList(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        return property.GetString() ?? string.Empty;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToList();
    }
}

/// <summary>
/// Background processor for an inbound Azure Monitor alert. Concrete
/// class injected directly via <c>GetRequiredService&lt;AlertProcessingService&gt;</c>
/// from <see cref="AlertProcessingQueue"/> — the
/// <c>IAlertProcessingService</c> interface that previously fronted this
/// class was a single-impl wrapper with no test double or alternate
/// implementation. Deleted by the 2026-05-05 single-impl audit.
/// </summary>
internal sealed class AlertProcessingService
{
    private readonly PlatformDbContext _dbContext;
    private readonly IAlertAnalysisWorkflow _analysisWorkflow;
    private readonly PlatformAdminAlertAudienceResolver _audienceResolver;
    private readonly INotificationService _notificationService;
    private readonly IClock _clock;
    private readonly ILogger<AlertProcessingService> _logger;

    public AlertProcessingService(
        PlatformDbContext dbContext,
        IAlertAnalysisWorkflow analysisWorkflow,
        PlatformAdminAlertAudienceResolver audienceResolver,
        INotificationService notificationService,
        IClock clock,
        ILogger<AlertProcessingService> logger)
    {
        _dbContext = dbContext;
        _analysisWorkflow = analysisWorkflow;
        _audienceResolver = audienceResolver;
        _notificationService = notificationService;
        _clock = clock;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid alertId, CancellationToken cancellationToken = default)
    {
        var alertEvent = await _dbContext.Set<AzureMonitorAlertEvent>()
            .FirstOrDefaultAsync(x => x.Id == alertId && x.TenantId == Guid.Empty, cancellationToken);

        if (alertEvent is null)
        {
            return;
        }

        if (alertEvent.Status == AzureMonitorAlertStatuses.Processed || alertEvent.Status == AzureMonitorAlertStatuses.Ignored)
        {
            return;
        }

        alertEvent.Status = AzureMonitorAlertStatuses.Processing;
        alertEvent.ProcessingAttempts += 1;
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var recipients = await _audienceResolver.ResolveUserIdsAsync(cancellationToken);
            var analysis = await _analysisWorkflow.AnalyzeAsync(alertEvent, cancellationToken);

            alertEvent.AiRunId = analysis.AiRunId;
            alertEvent.AnalysisSummary = analysis.Summary;
            alertEvent.AnalysisJson = analysis.ToJson();
            alertEvent.ProcessedAtUtc = _clock.UtcNow;
            alertEvent.LastError = null;

            if (recipients.Count == 0)
            {
                alertEvent.Status = AzureMonitorAlertStatuses.Ignored;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            var notificationTitle = BuildNotificationTitle(alertEvent);
            var notificationBody = BuildNotificationBody(alertEvent, analysis);

            await _notificationService.CreateForUsersAsync(
                new Aonik.Platform.Contracts.Models.Notifications.CreateNotificationsRequest(
                    TenantId: Guid.Empty,
                    UserIds: recipients,
                    Type: alertEvent.NormalizedType,
                    Source: AzureMonitorAlertProviders.AzureMonitor,
                    Title: notificationTitle,
                    Body: notificationBody,
                    Severity: MapNotificationSeverity(alertEvent),
                    ActionUrl: $"/admin/alerts/{alertEvent.Id}",
                    CorrelationId: alertEvent.CorrelationKey,
                    AiRunId: alertEvent.AiRunId,
                    MetadataJson: JsonSerializer.Serialize(new
                    {
                        alertEvent.Id,
                        alertEvent.ExternalAlertId,
                        alertEvent.AlertRuleName,
                        alertEvent.MonitorCondition,
                        alertEvent.CorrelationKey,
                    })),
                cancellationToken);

            alertEvent.Status = AzureMonitorAlertStatuses.Processed;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing failed for Azure Monitor alert {AlertId}.", alertId);
            alertEvent.Status = AzureMonitorAlertStatuses.Failed;
            alertEvent.LastError = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            alertEvent.ProcessedAtUtc = _clock.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static string BuildNotificationTitle(AzureMonitorAlertEvent alertEvent)
    {
        var isResolved = string.Equals(alertEvent.MonitorCondition, AzureMonitorAlertConditions.Resolved, StringComparison.OrdinalIgnoreCase);
        return isResolved
            ? $"Resolved: {alertEvent.AlertRuleName}"
            : $"Alert: {alertEvent.AlertRuleName}";
    }

    private static string BuildNotificationBody(AzureMonitorAlertEvent alertEvent, AlertAnalysisResult analysis)
    {
        var resources = JsonSerializer.Deserialize<List<string>>(alertEvent.ResourceIdsJson) ?? [];
        var resourceLine = resources.Count == 0 ? string.Empty : $"Affected resource: {resources[0]}\n";

        return string.Join("\n", new[]
        {
            analysis.Summary,
            resourceLine.TrimEnd(),
            string.IsNullOrWhiteSpace(analysis.LikelyCause) ? string.Empty : $"Likely cause: {analysis.LikelyCause}",
            string.IsNullOrWhiteSpace(analysis.Impact) ? string.Empty : $"Impact: {analysis.Impact}",
            analysis.RecommendedActions.Count == 0 ? string.Empty : $"Next actions: {string.Join("; ", analysis.RecommendedActions)}",
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string MapNotificationSeverity(AzureMonitorAlertEvent alertEvent)
    {
        if (string.Equals(alertEvent.MonitorCondition, AzureMonitorAlertConditions.Resolved, StringComparison.OrdinalIgnoreCase))
        {
            return NotificationSeverities.Success;
        }

        return alertEvent.Severity.ToUpperInvariant() switch
        {
            "SEV0" or "SEV1" or "SEV2" => NotificationSeverities.Error,
            "SEV3" => NotificationSeverities.Warning,
            _ => NotificationSeverities.Info,
        };
    }
}
