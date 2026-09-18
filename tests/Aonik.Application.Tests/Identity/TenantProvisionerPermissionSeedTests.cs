using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Identity;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Aonik.Application.Tests.Identity;

/// <summary>
/// Spec 026 §10/§13: the lifecycle-closure permissions must be granted through the canonical
/// <see cref="TenantProvisioner"/> path — <c>Users.Delete</c> to TenantAdmin only, and
/// <c>Users.RevokeSessions</c> to both TenantAdmin and Operations. The service layer enforces these
/// (<c>AccessManagementService</c> calls <c>EnsurePermissionAsync</c>), so a tenant provisioned without
/// the grant would get a 403 on hard-delete / revoke-sessions. Guards the seed maps from re-drifting.
/// </summary>
public class TenantProvisionerPermissionSeedTests
{
    private static readonly Guid TenantId = Guid.Parse("e0000000-0000-0000-0000-000000000026");
    private static readonly Guid ActorId = Guid.Parse("e0000000-0000-0000-0000-0000000000aa");
    private static readonly DateTime Now = new(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);

    private delegate void TryGetTenantDelegate(out Guid tenantId);

    [Fact]
    public async Task Provision_Grants_Spec026_UserDelete_And_RevokeSessions_ToTheRightRoles()
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(x => x.GetCurrentTenantId()).Returns(TenantId);
        tenantProvider
            .Setup(x => x.TryGetCurrentTenantId(out It.Ref<Guid>.IsAny))
            .Callback(new TryGetTenantDelegate((out Guid id) => id = TenantId))
            .Returns(true);

        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.Setup(x => x.GetCurrentUserId()).Returns(ActorId);

        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow).Returns(Now);

        var correlation = new Mock<ICorrelationContext>();
        correlation.SetupGet(x => x.CorrelationId).Returns("corr-026");

        var permissionService = new Mock<IPermissionService>();
        permissionService
            .Setup(x => x.HasPermissionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var auditLog = new Mock<IAuditLogWriter>();

        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"TenantProvisionerPermSeed_{Guid.NewGuid()}")
            .Options;
        await using var db = new PlatformDbContext(options, tenantProvider.Object, currentUser.Object, clock.Object);

        db.Tenants.Add(new Tenant { Id = TenantId, Name = "Acme", DefaultCurrency = "GBP", Status = "Active" });
        await db.SaveChangesAsync();

        var configPackApplier = new Mock<Aonik.Platform.Contracts.Services.Packs.IConfigPackApplier>();
        configPackApplier
            .Setup(a => a.ApplyAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Aonik.Platform.Contracts.Services.Packs.ConfigPackResult.None);
        // Spec 097 §13 — the module step runs before the contributor loop; a base tenant writes no rows.
        configPackApplier
            .Setup(a => a.ApplyModulesAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(sp => sp.GetService(typeof(Aonik.Platform.Contracts.Services.Packs.IConfigPackApplier)))
            .Returns(configPackApplier.Object);

        var provisioner = new TenantProvisioner(
            db,
            auditLog.Object,
            clock.Object,
            currentUser.Object,
            correlation.Object,
            permissionService.Object,
            Array.Empty<ITenantProvisioningContributor>(),
            serviceProvider.Object);

        // The bootstrap path provisions without the Tenants.Write precheck, exercising the real
        // catalogue + role-permission seeding that the endpoint path relies on.
        await ((IBootstrapTenantProvisioner)provisioner).ProvisionTenantAsync(TenantId);

        var adminKeys = await GrantedKeysAsync(db, "TenantAdmin");
        adminKeys.Should().Contain("Users.Delete");
        adminKeys.Should().Contain("Users.RevokeSessions");

        var opsKeys = await GrantedKeysAsync(db, "Operations");
        opsKeys.Should().Contain("Users.RevokeSessions");
        opsKeys.Should().NotContain("Users.Delete"); // §13: hard-delete is never auto-granted to Operations
    }

    private static async Task<HashSet<string>> GrantedKeysAsync(PlatformDbContext db, string roleName)
    {
        var role = await db.Roles.FirstAsync(r => r.TenantId == TenantId && r.Name == roleName);
        var permissionIds = await db.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .Select(rp => rp.PermissionId)
            .ToListAsync();
        var keys = await db.Permissions
            .Where(p => permissionIds.Contains(p.Id))
            .Select(p => p.Key)
            .ToListAsync();
        return keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
