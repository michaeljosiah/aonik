using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Operations;

public class Job : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string? ScheduleCron { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? LastRunAt { get; set; }
    public string LastResultJson { get; set; } = string.Empty;
}
