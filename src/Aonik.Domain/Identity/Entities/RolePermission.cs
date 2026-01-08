using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Identity.Entities;

public class RolePermission : AuditableEntity
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
    public Role Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
