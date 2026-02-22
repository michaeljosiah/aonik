using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Identity;

public class Role : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<RolePermission> RolePermissions { get; set; } = new();
}
