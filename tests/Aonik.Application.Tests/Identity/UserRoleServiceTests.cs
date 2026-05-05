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
/// Unit tests for <see cref="UserRoleService"/> covering the assign/
/// remove/get role surface area, idempotency, tenant boundary checks,
/// and the audit-log + permission-gate side effects.
/// xUnit + Moq + FluentAssertions per the project's standard testing stack.
/// </summary>
public class UserRoleServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("d1000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = Guid.Parse("d2000000-0000-0000-0000-000000000002");
    private static readonly Guid CallingUserId = Guid.Parse("d1100000-0000-0000-0000-000000000099");
    private static readonly Guid TargetUserId = Guid.Parse("d1200000-0000-0000-0000-000000000010");
    private static readonly Guid RoleId = Guid.Parse("d1300000-0000-0000-0000-000000000020");
    private static readonly DateTime FixedNow = new(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ITenantProvider> _tenantProvider;
    private readonly Mock<ICurrentUserProvider> _currentUserProvider;
    private readonly Mock<IPermissionService> _permissionService;
    private readonly Mock<IAuditLogWriter> _auditLogWriter;
    private readonly Mock<IClock> _clock;
    private readonly Mock<ICorrelationContext> _correlationContext;

    public UserRoleServiceTests()
    {
        _tenantProvider = new Mock<ITenantProvider>();
        _tenantProvider.Setup(x => x.GetCurrentTenantId()).Returns(TenantId);
        _tenantProvider
            .Setup(x => x.TryGetCurrentTenantId(out It.Ref<Guid>.IsAny))
            .Callback(new TryGetCurrentTenantIdDelegate((out Guid id) => id = TenantId))
            .Returns(true);

        _currentUserProvider = new Mock<ICurrentUserProvider>();
        _currentUserProvider.Setup(x => x.GetCurrentUserId()).Returns(CallingUserId);

        // Default to permissive — individual tests override to test denial.
        _permissionService = new Mock<IPermissionService>();
        _permissionService
            .Setup(x => x.HasPermissionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _auditLogWriter = new Mock<IAuditLogWriter>();
        _clock = new Mock<IClock>();
        _clock.SetupGet(x => x.UtcNow).Returns(FixedNow);
        _correlationContext = new Mock<ICorrelationContext>();
        _correlationContext.SetupGet(x => x.CorrelationId).Returns("corr-123");
    }

    private delegate void TryGetCurrentTenantIdDelegate(out Guid tenantId);

    // ── GetUserRolesAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetUserRolesAsync_Should_ReturnRoles_For_UserInTenant()
    {
        await using var dbContext = CreateDbContext();
        SeedUser(dbContext, TargetUserId, TenantId);
        var role = SeedRole(dbContext, RoleId, TenantId, "TenantAdmin");
        SeedUserRole(dbContext, TargetUserId, RoleId);
        await dbContext.SaveChangesAsync();

        var service = NewService(dbContext);

        var response = await service.GetUserRolesAsync(TargetUserId);

        response.UserId.Should().Be(TargetUserId);
        response.Roles.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { RoleId = role.Id, Name = "TenantAdmin" });
    }

    [Fact]
    public async Task GetUserRolesAsync_Should_Throw_When_UserNotInTenant()
    {
        await using var dbContext = CreateDbContext();
        SeedUser(dbContext, TargetUserId, OtherTenantId); // different tenant
        await dbContext.SaveChangesAsync();

        var service = NewService(dbContext);

        var act = async () => await service.GetUserRolesAsync(TargetUserId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"User {TargetUserId} not found in tenant {TenantId}");
    }

    [Fact]
    public async Task GetUserRolesAsync_Should_Throw_PermissionDenied_When_UsersReadMissing()
    {
        await using var dbContext = CreateDbContext();
        SeedUser(dbContext, TargetUserId, TenantId);
        await dbContext.SaveChangesAsync();

        _permissionService
            .Setup(x => x.HasPermissionAsync(CallingUserId, "Users.Read", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = NewService(dbContext);

        var act = async () => await service.GetUserRolesAsync(TargetUserId);

        var ex = await act.Should().ThrowAsync<PermissionDeniedException>();
        ex.Which.PermissionKey.Should().Be("Users.Read");
    }

    // ── AssignRoleAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task AssignRoleAsync_Should_Persist_UserRole_And_WriteAuditLog()
    {
        await using var dbContext = CreateDbContext();
        SeedUser(dbContext, TargetUserId, TenantId);
        SeedRole(dbContext, RoleId, TenantId, "Operations");
        await dbContext.SaveChangesAsync();

        var service = NewService(dbContext);

        var response = await service.AssignRoleAsync(TargetUserId, RoleId);

        response.Roles.Should().ContainSingle().Which.Name.Should().Be("Operations");

        var assigned = await dbContext.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == TargetUserId && ur.RoleId == RoleId);
        assigned.Should().NotBeNull();
        assigned!.CreatedAt.Should().Be(FixedNow);
        assigned.CreatedBy.Should().Be(CallingUserId);

        _auditLogWriter.Verify(
            x => x.LogAsync(
                "UserRoleAssigned",
                "UserRole",
                It.IsAny<Guid>(),
                TenantId,
                CallingUserId,
                "corr-123",
                It.Is<string>(json => json.Contains("Operations") && json.Contains(TargetUserId.ToString())),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AssignRoleAsync_Should_BeIdempotent_When_UserAlreadyHasRole()
    {
        await using var dbContext = CreateDbContext();
        SeedUser(dbContext, TargetUserId, TenantId);
        SeedRole(dbContext, RoleId, TenantId, "Operations");
        SeedUserRole(dbContext, TargetUserId, RoleId);
        await dbContext.SaveChangesAsync();

        var service = NewService(dbContext);

        var response = await service.AssignRoleAsync(TargetUserId, RoleId);

        response.Roles.Should().HaveCount(1);

        // Idempotent: no new UserRole row, no audit-log entry written.
        var rowCount = await dbContext.UserRoles
            .CountAsync(ur => ur.UserId == TargetUserId && ur.RoleId == RoleId);
        rowCount.Should().Be(1);

        _auditLogWriter.Verify(
            x => x.LogAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AssignRoleAsync_Should_Throw_When_RoleBelongsToDifferentTenant()
    {
        await using var dbContext = CreateDbContext();
        SeedUser(dbContext, TargetUserId, TenantId);
        SeedRole(dbContext, RoleId, OtherTenantId, "Operations");
        await dbContext.SaveChangesAsync();

        var service = NewService(dbContext);

        var act = async () => await service.AssignRoleAsync(TargetUserId, RoleId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Role {RoleId} not found in tenant {TenantId}");
    }

    [Fact]
    public async Task AssignRoleAsync_Should_Throw_PermissionDenied_When_UsersManageMissing()
    {
        await using var dbContext = CreateDbContext();
        SeedUser(dbContext, TargetUserId, TenantId);
        SeedRole(dbContext, RoleId, TenantId, "Operations");
        await dbContext.SaveChangesAsync();

        _permissionService
            .Setup(x => x.HasPermissionAsync(CallingUserId, "Users.Manage", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = NewService(dbContext);

        var act = async () => await service.AssignRoleAsync(TargetUserId, RoleId);

        var ex = await act.Should().ThrowAsync<PermissionDeniedException>();
        ex.Which.PermissionKey.Should().Be("Users.Manage");
    }

    // ── RemoveRoleAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task RemoveRoleAsync_Should_SoftDelete_UserRole_And_WriteAuditLog()
    {
        await using var dbContext = CreateDbContext();
        SeedUser(dbContext, TargetUserId, TenantId);
        SeedRole(dbContext, RoleId, TenantId, "Operations");
        SeedUserRole(dbContext, TargetUserId, RoleId);
        await dbContext.SaveChangesAsync();

        var service = NewService(dbContext);

        await service.RemoveRoleAsync(TargetUserId, RoleId);

        // AonikDbContextBase converts EntityState.Deleted into a soft-delete
        // (IsDeleted = true, DeletedAt/By stamped). The row is still in the
        // table; the platform's tenant-scoped query filter would normally
        // hide it from reads, but UserRole is NOT ITenantScoped so the filter
        // doesn't apply here. Asserting on the soft-delete fields directly
        // captures the actual behaviour.
        var stored = await dbContext.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == TargetUserId && ur.RoleId == RoleId);
        stored.Should().NotBeNull();
        stored!.IsDeleted.Should().BeTrue();
        stored.DeletedAt.Should().Be(FixedNow);
        stored.DeletedBy.Should().Be(CallingUserId);

        _auditLogWriter.Verify(
            x => x.LogAsync(
                "UserRoleRemoved",
                "UserRole",
                It.IsAny<Guid>(),
                TenantId,
                CallingUserId,
                "corr-123",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveRoleAsync_Should_BeNoop_When_UserDoesNotHaveTheRole()
    {
        await using var dbContext = CreateDbContext();
        SeedUser(dbContext, TargetUserId, TenantId);
        SeedRole(dbContext, RoleId, TenantId, "Operations");
        // No UserRole seed — user does NOT have the role to begin with.
        await dbContext.SaveChangesAsync();

        var service = NewService(dbContext);

        var response = await service.RemoveRoleAsync(TargetUserId, RoleId);

        response.Roles.Should().BeEmpty();
        _auditLogWriter.Verify(
            x => x.LogAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private UserRoleService NewService(PlatformDbContext dbContext)
        => new(
            dbContext,
            _tenantProvider.Object,
            _auditLogWriter.Object,
            _clock.Object,
            _currentUserProvider.Object,
            _correlationContext.Object,
            _permissionService.Object);

    private PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"UserRoleService_{Guid.NewGuid()}")
            .Options;

        // Pass the mocked clock + user provider so AonikDbContextBase.
        // UpdateAuditFields stamps deterministic CreatedAt / DeletedAt
        // values that the tests can assert against.
        return new PlatformDbContext(
            options,
            _tenantProvider.Object,
            _currentUserProvider.Object,
            _clock.Object);
    }

    private static void SeedUser(PlatformDbContext db, Guid userId, Guid tenantId)
    {
        db.Users.Add(new User
        {
            Id = userId,
            TenantId = tenantId,
            ExternalIssuer = "test-issuer",
            ExternalSubject = userId.ToString("N"),
            Email = $"{userId:N}@example.com",
            Status = "Active",
        });
    }

    private static Role SeedRole(PlatformDbContext db, Guid roleId, Guid tenantId, string name)
    {
        var role = new Role
        {
            Id = roleId,
            TenantId = tenantId,
            Name = name,
        };
        db.Roles.Add(role);
        return role;
    }

    private static void SeedUserRole(PlatformDbContext db, Guid userId, Guid roleId)
    {
        db.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = roleId,
            CreatedAt = DateTime.UtcNow,
        });
    }
}
