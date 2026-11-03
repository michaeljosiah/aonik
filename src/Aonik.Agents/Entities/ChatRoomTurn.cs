using Aonik.SharedKernel.Primitives;

namespace Aonik.Agents.Entities;

public class ChatRoomTurn : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid RoomId { get; set; }
    public Guid ClientRequestId { get; set; }
    public Guid PromptMessageId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string Status { get; set; } = "Running";
    public Guid? AiRunId { get; set; }
    public string ApprovalIdsJson { get; set; } = "[]";
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
