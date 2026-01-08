using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Identity.Entities;

public class Role : AuditableEntity, ITenantScoped
{
    public Guid RoleId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    private readonly List<RolePermission> _rolePermissions = new();
    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions.AsReadOnly();

    private Role() { }

    public Role(Guid tenantId, string name)
    {
        RoleId = Id;
        TenantId = tenantId;
        Name = name;
    }

    public void UpdateName(string name)
    {
        Name = name;
    }

    public void AddPermission(Permission permission)
    {
        if (_rolePermissions.Any(rp => rp.PermissionId == permission.Id))
            return;

        var rolePermission = new RolePermission(RoleId, permission.Id);
        _rolePermissions.Add(rolePermission);
    }

    public void RemovePermission(Guid permissionId)
    {
        var rolePermission = _rolePermissions.FirstOrDefault(rp => rp.PermissionId == permissionId);
        if (rolePermission != null)
        {
            _rolePermissions.Remove(rolePermission);
        }
    }
}
