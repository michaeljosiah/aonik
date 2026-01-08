using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Operations.Entities;

public class WorkItem : AuditableEntity, ITenantScoped
{
    public Guid WorkItemId { get; private set; }
    public Guid TenantId { get; private set; }
    public string WorkItemType { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string Priority { get; private set; } = string.Empty;
    public Guid? AssignedToUserId { get; private set; }
    public DateTime? SlaDueAt { get; private set; }
    public string ContextType { get; private set; } = string.Empty;
    public Guid ContextId { get; private set; }
    public string HistoryJson { get; private set; } = string.Empty;

    private WorkItem() { }

    public WorkItem(Guid tenantId, string workItemType, string priority, string contextType, Guid contextId, DateTime? slaDueAt = null)
    {
        WorkItemId = Id;
        TenantId = tenantId;
        WorkItemType = workItemType;
        Priority = priority;
        ContextType = contextType;
        ContextId = contextId;
        SlaDueAt = slaDueAt;
        Status = "Pending";
        HistoryJson = "[]";
    }

    public void AssignTo(Guid userId)
    {
        AssignedToUserId = userId;
        Status = "Assigned";
    }

    public void Unassign()
    {
        AssignedToUserId = null;
        Status = "Pending";
    }

    public void UpdateStatus(string status)
    {
        Status = status;
    }

    public void Complete()
    {
        Status = "Completed";
    }

    public void UpdateHistory(string historyJson)
    {
        HistoryJson = historyJson;
    }
}
