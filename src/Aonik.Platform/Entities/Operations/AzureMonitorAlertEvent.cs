using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Operations;

public class AzureMonitorAlertEvent : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Provider { get; set; } = AzureMonitorAlertProviders.AzureMonitor;
    public string ExternalAlertId { get; set; } = string.Empty;
    public string AlertRuleName { get; set; } = string.Empty;
    public string AlertRuleId { get; set; } = string.Empty;
    public string MonitorCondition { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string SignalType { get; set; } = string.Empty;
    public string MonitoringService { get; set; } = string.Empty;
    public string NormalizedType { get; set; } = string.Empty;
    public string CorrelationKey { get; set; } = string.Empty;
    public string Status { get; set; } = AzureMonitorAlertStatuses.Received;
    public string ResourceIdsJson { get; set; } = "[]";
    public string EssentialsJson { get; set; } = "{}";
    public string AlertContextJson { get; set; } = "{}";
    public string CustomPropertiesJson { get; set; } = "{}";
    public string AnalysisSummary { get; set; } = string.Empty;
    public string AnalysisJson { get; set; } = "{}";
    public Guid? AiRunId { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime? FiredAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public int ProcessingAttempts { get; set; }
    public string? LastError { get; set; }
}
