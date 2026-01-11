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
        return this;
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
