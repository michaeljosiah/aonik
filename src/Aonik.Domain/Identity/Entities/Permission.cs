using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Identity.Entities;

public class Permission : AuditableEntity
{
    public Guid PermissionId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    private Permission() { }

    public Permission(string key, string? description = null)
    {
        PermissionId = Id;
        Key = key;
        Description = description;
    }

    public void UpdateKey(string key)
    {
        Key = key;
    }

    public void UpdateDescription(string? description)
    {
        Description = description;
    }
}
