using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Integrations;

public sealed class ConnectorConnectionGrant : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ConnectionId { get; set; }
    public string GranteeType { get; set; } = string.Empty;
    public Guid GranteeId { get; set; }
    public string AllowedActionsJson { get; set; } = "[]";
    public Guid IssuedByUserId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? RevokedByUserId { get; set; }
}
