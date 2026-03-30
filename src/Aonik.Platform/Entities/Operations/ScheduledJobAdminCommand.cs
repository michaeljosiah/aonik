using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Operations;

public class ScheduledJobAdminCommand : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string CommandType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public Guid? RequestedByUserId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ResultMessage { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
}
