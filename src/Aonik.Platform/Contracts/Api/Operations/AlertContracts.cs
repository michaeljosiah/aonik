namespace Aonik.Platform.Contracts.Api.Operations;

public record AzureMonitorAlertWebhookRequest(
    string? SchemaId,
    AzureMonitorAlertWebhookData? Data);

public record AzureMonitorAlertWebhookData(
    AzureMonitorAlertEssentials? Essentials,
    object? AlertContext,
    Dictionary<string, string>? CustomProperties);

public record AzureMonitorAlertEssentials(
    string? AlertId,
    string? AlertRule,
    string? Severity,
    string? SignalType,
    string? MonitorCondition,
    string? MonitoringService,
    string[]? AlertTargetIDs,
    string[]? ConfigurationItems,
    string? FiredDateTime,
    string? ResolvedDateTime,
    string? Description,
    string? InvestigationLink,
    string? EssentialsVersion,
    string? AlertContextVersion,
    string? OriginAlertId);

public record AlertWebhookAcceptedResponse(
    Guid AlertId,
    string Status);

public record AlertListResponse(
    IReadOnlyList<AlertSummaryResponse> Alerts);

public record AlertSummaryResponse(
    Guid Id,
    string AlertRuleName,
    string MonitorCondition,
    string Severity,
    string SignalType,
    string NormalizedType,
    string Status,
    string AnalysisSummary,
    DateTime ReceivedAtUtc,
    DateTime? FiredAtUtc,
    DateTime? ResolvedAtUtc,
    IReadOnlyList<string> ResourceIds);

public record AlertAnalysisResponse(
    string Summary,
    string LikelyCause,
    string Impact,
    string AffectedComponent,
    IReadOnlyList<string> RecommendedActions,
    string Confidence);

public record AlertDetailResponse(
    Guid Id,
    string Provider,
    string ExternalAlertId,
    string AlertRuleName,
    string AlertRuleId,
    string MonitorCondition,
    string Severity,
    string SignalType,
    string MonitoringService,
    string NormalizedType,
    string Status,
    string CorrelationKey,
    DateTime ReceivedAtUtc,
    DateTime? FiredAtUtc,
    DateTime? ResolvedAtUtc,
    DateTime? ProcessedAtUtc,
    Guid? AiRunId,
    string Description,
    string InvestigationLink,
    IReadOnlyList<string> ResourceIds,
    IReadOnlyDictionary<string, string> CustomProperties,
    AlertAnalysisResponse? Analysis,
    string EssentialsJson,
    string AlertContextJson);
