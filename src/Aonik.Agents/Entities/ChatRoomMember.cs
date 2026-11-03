using Aonik.SharedKernel.Primitives;

namespace Aonik.Agents.Entities;

public class ChatRoomMember : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid RoomId { get; set; }
    public string ActorKey { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string? AgentName { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "Participant";
    public bool IsActive { get; set; } = true;
    public DateTime? LastSeenAt { get; set; }
}
