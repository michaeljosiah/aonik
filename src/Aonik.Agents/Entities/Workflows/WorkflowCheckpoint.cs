using Aonik.SharedKernel.Primitives;

namespace Aonik.Agents.Entities.Workflows;

public class WorkflowCheckpoint : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid RunId { get; set; }
    public Guid WorkflowVersionId { get; set; }
    public string FrameworkSessionId { get; set; } = string.Empty;
    public string CheckpointId { get; set; } = string.Empty;
    public string? ParentCheckpointId { get; set; }
    public long Sequence { get; set; }
    public int FormatVersion { get; set; }
    public string FrameworkVersion { get; set; } = string.Empty;
    public string StateJson { get; set; } = string.Empty;
}
