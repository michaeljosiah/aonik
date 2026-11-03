using Aonik.SharedKernel.Primitives;

namespace Aonik.Agents.Entities;

public class ChatRoom : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ContextJson { get; set; } = "[]";
    public long Revision { get; set; }
    public long LastSequence { get; set; }
    public DateTime LastActivityAt { get; set; }
    public Guid? ActiveTurnId { get; set; }
}
