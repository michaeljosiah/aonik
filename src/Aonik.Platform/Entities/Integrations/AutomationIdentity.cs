using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Integrations;

public sealed class AutomationIdentity : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid AccountableUserId { get; set; }
    public Guid ConfiguredByUserId { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime? ExpiresAt { get; set; }
    public DateTime? DisabledAt { get; set; }
    public Guid? DisabledByUserId { get; set; }
    public string GrantsJson { get; set; } = "[]";
    public string PermissionsJson { get; set; } = "[]";
}
