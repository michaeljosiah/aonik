using System.Security.Claims;

namespace Aonik.Api.Tests;

public sealed class TestAuthOptions
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; } = Guid.NewGuid();
    public List<string> Roles { get; } = new();
    public List<string> Permissions { get; } = new();
    public List<Claim> Claims { get; } = new();

    public static TestAuthOptions Create() => new();

    public TestAuthOptions WithPermissions(params string[] permissions)
    {
        Permissions.AddRange(permissions);
        if (permissions.Length > 0)
        {
            Claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        }
        return this;
    }

    [Obsolete("Use WithPermissions for API tests.")]
    public TestAuthOptions WithTestRolePermissions(params string[] permissions)
    {
        return WithPermissions(permissions);
    }



    public TestAuthOptions WithTenant(Guid tenantId)
    {
        TenantId = tenantId;
        return this;
    }

    public TestAuthOptions WithRoles(params string[] roles)
    {
        Roles.AddRange(roles);
        return this;
    }

    public TestAuthOptions WithClaims(params Claim[] claims)
    {
        Claims.AddRange(claims);
        return this;
    }
}
