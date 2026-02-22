using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Identity;

public class Permission : AuditableEntity
{
    public string Key { get; set; } = string.Empty;
    public string? Description { get; set; }
}
