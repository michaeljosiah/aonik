using Microsoft.AspNetCore.Authorization;

namespace Aonik.Infrastructure.Authorization;

public class RoleOrPermissionRequirement : IAuthorizationRequirement
{
    public RoleOrPermissionRequirement(IReadOnlyCollection<string> roleNames, IReadOnlyCollection<string> permissionKeys)
    {
        RoleNames = roleNames;
        PermissionKeys = permissionKeys;
    }

    public IReadOnlyCollection<string> RoleNames { get; }

    public IReadOnlyCollection<string> PermissionKeys { get; }
}
