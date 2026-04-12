using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Operations;

public class ScheduledJobProjection : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "UTC";
    public string State { get; set; } = string.Empty;
    public DateTime? NextFireTimeUtc { get; set; }
    public DateTime? PreviousFireTimeUtc { get; set; }
    public string? LastOutcome { get; set; }
    public string? LastOutcomeSummary { get; set; }
    public int? LastDurationMs { get; set; }
    public DateTime LastSyncedAtUtc { get; set; }

    /// <summary>
    /// Runtime-editable job configuration stored as JSON.
    /// Managed via the Admin UI; takes precedence over appsettings defaults.
    /// </summary>
    public string? ConfigurationJson { get; set; }
}
