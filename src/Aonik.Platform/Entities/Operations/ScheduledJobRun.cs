using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Operations;

public class ScheduledJobRun : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int DurationMs { get; set; }
    public string TriggeredBy { get; set; } = string.Empty;
    public DateTime FiredAtUtc { get; set; }
    public DateTime CompletedAtUtc { get; set; }
    public string? FireInstanceId { get; set; }
}
