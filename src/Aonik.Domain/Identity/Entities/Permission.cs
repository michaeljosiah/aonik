using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Identity.Entities;

public class Permission : AuditableEntity
{
    public Guid PermissionId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string? Description { get; set; }
}
