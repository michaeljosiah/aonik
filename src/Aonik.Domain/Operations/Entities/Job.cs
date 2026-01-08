using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Operations.Entities;

public class Job : AuditableEntity, ITenantScoped
{
    public Guid JobId { get; private set; }
    public Guid TenantId { get; private set; }
    public string JobType { get; private set; } = string.Empty;
    public string? ScheduleCron { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public DateTime? LastRunAt { get; private set; }
    public string LastResultJson { get; private set; } = string.Empty;

    private Job() { }

    public Job(Guid tenantId, string jobType, string? scheduleCron = null)
    {
        JobId = Id;
        TenantId = tenantId;
        JobType = jobType;
        ScheduleCron = scheduleCron;
        Status = "Active";
        LastResultJson = "{}";
    }

    public void UpdateSchedule(string scheduleCron)
    {
        ScheduleCron = scheduleCron;
    }

    public void UpdateStatus(string status)
    {
        Status = status;
    }

    public void RecordRun(string resultJson)
    {
        LastRunAt = DateTime.UtcNow;
        LastResultJson = resultJson;
    }

    public void Activate()
    {
        Status = "Active";
    }

    public void Deactivate()
    {
        Status = "Inactive";
    }
}
