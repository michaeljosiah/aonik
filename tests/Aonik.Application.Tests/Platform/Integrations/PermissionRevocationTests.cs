using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Identity;

namespace Aonik.Application.Tests.Platform.Integrations;

public class PermissionRevocationTests
{
    [Theory]
    [InlineData("membership")]
    [InlineData("role-permission")]
    [InlineData("permission")]
    [InlineData("role")]
    [InlineData("user")]
    public async Task SoftDeletedAuthority_Should_NotRemainAnEffectivePermission(string removed)
    {
        using var scenario = new ConnectorScenario();
        await scenario.InitializeAsync();
        using IServiceScope scope = scenario.Scope();
        PlatformDbContext db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var permission = new Permission { Key = "Workflow.Write" };
        var link = new RolePermission { RoleId = scenario.ReviewerRoleId, PermissionId = permission.Id, Permission = permission };
        db.Permissions.Add(permission); db.RolePermissions.Add(link); await db.SaveChangesAsync();
        var service = new PermissionService(db);
        (await service.HasPermissionAsync(scenario.ReviewerId, permission.Key)).Should().BeTrue();

        if (removed == "membership") db.UserRoles.Remove(await db.UserRoles.SingleAsync(item => item.UserId == scenario.ReviewerId));
        if (removed == "role-permission") db.RolePermissions.Remove(link);
        if (removed == "permission") permission.IsDeleted = true;
        if (removed == "role") db.Roles.Remove(await db.Roles.SingleAsync(item => item.Id == scenario.ReviewerRoleId));
        if (removed == "user") db.Users.Remove(await db.Users.SingleAsync(item => item.Id == scenario.ReviewerId));
        await db.SaveChangesAsync();

        (await service.HasPermissionAsync(scenario.ReviewerId, permission.Key)).Should().BeFalse();
        (await service.GetUserPermissionsAsync(scenario.ReviewerId)).Should().BeEmpty();
    }
}
