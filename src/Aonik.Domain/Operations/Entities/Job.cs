using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Operations.Entities;

public class Job : AuditableEntity, ITenantScoped
{
    public Guid JobId { get; set; }
    public Guid TenantId { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string? ScheduleCron { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? LastRunAt { get; set; }
    public string LastResultJson { get; set; } = string.Empty;
}
