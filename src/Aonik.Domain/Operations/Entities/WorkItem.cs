using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Operations.Entities;

public class WorkItem : AuditableEntity, ITenantScoped
{
    public Guid WorkItemId { get; set; }
    public Guid TenantId { get; set; }
    public string WorkItemType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public Guid? AssignedToUserId { get; set; }
    public DateTime? SlaDueAt { get; set; }
    public string ContextType { get; set; } = string.Empty;
    public Guid ContextId { get; set; }
    public string HistoryJson { get; set; } = string.Empty;
}
