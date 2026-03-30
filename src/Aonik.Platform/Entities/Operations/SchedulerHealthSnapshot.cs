using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Operations;

public class SchedulerHealthSnapshot : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string SchedulerName { get; set; } = string.Empty;
    public string SchedulerInstanceId { get; set; } = string.Empty;
    public bool IsStarted { get; set; }
    public bool InStandbyMode { get; set; }
    public int ThreadPoolSize { get; set; }
    public int ActiveJobCount { get; set; }
    public int TotalJobCount { get; set; }
    public int TotalTriggerCount { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}
