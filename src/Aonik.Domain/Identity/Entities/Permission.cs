using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Identity.Entities;

public class Permission : AuditableEntity
{
    public string Key { get; set; } = string.Empty;
    public string? Description { get; set; }
}
