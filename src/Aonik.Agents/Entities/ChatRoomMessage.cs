using Aonik.SharedKernel.Primitives;

namespace Aonik.Agents.Entities;

public class ChatRoomMessage : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid RoomId { get; set; }
    public long Sequence { get; set; }
    public Guid? ClientRequestId { get; set; }
    public string ActorKind { get; set; } = "Human";
    public Guid? AuthorUserId { get; set; }
    public string? AgentName { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string MentionKeysJson { get; set; } = "[]";
    public Guid? AiRunId { get; set; }
    public Guid? TurnId { get; set; }
}
