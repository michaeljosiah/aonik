using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Identity.Entities;

public class Role : AuditableEntity, ITenantScoped
{
    public Guid RoleId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<RolePermission> RolePermissions { get; set; } = new();
}
