using Microsoft.AspNetCore.Authorization;

namespace Aonik.Infrastructure.Authorization;

public class PermissionRequirement : IAuthorizationRequirement
{
    public string PermissionKey { get; }

    public PermissionRequirement(string permissionKey)
    {
        PermissionKey = permissionKey ?? throw new ArgumentNullException(nameof(permissionKey));
    }
}
