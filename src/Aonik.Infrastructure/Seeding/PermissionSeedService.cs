using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Observability;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Seeding;
using Aonik.Application.Services;
using Aonik.Application.Services.Compliance;
using Aonik.Application.Services.Identity;
using Aonik.Application.Services.Seeding;
using Aonik.Infrastructure.Persistence.Seed;
using Aonik.SharedKernel.Abstractions;
using Aonik.Domain.Identity.Entities;

namespace Aonik.Infrastructure.Seeding;

public class PermissionSeedService : AdminServiceBase, IPermissionSeedService
{
    private readonly IAonikDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICorrelationContext _correlationContext;
    private readonly ITenantContext _tenantContext;

    public PermissionSeedService(
        IAonikDbContext dbContext,
        IClock clock,
        ILoggerFactory loggerFactory,
        IAuditLogWriter auditLogWriter,
        ICurrentUserProvider currentUserProvider,
        ICorrelationContext correlationContext,
        IPermissionService permissionService,
        ITenantContext tenantContext)
        : base(currentUserProvider, permissionService)
    {
        _dbContext = dbContext;
        _clock = clock;
        _loggerFactory = loggerFactory;
        _auditLogWriter = auditLogWriter;
        _correlationContext = correlationContext;
        _tenantContext = tenantContext;
    }

    public async Task<PermissionSeedResult> SeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await EnsureSeedPermissionAsync(cancellationToken);

        var tenantExists = await _dbContext.Tenants.AnyAsync(t => t.Id == tenantId, cancellationToken);
        if (!tenantExists)
        {
            throw new InvalidOperationException($"Tenant {tenantId} not found");
        }

        _tenantContext.TenantId = tenantId;
        _tenantContext.ResolutionSource = "AdminTenantAction";

        var operations = new List<string>();

        var identitySeed = new IdentitySeedService((IAonikDbContext)_dbContext, _loggerFactory.CreateLogger<IdentitySeedService>());
        await identitySeed.SeedAsync(cancellationToken);
        operations.Add("IdentitySeed");

        var now = _clock.UtcNow;
        var userId = CurrentUserProvider.GetCurrentUserId();

        var rolesCreated = await EnsureDefaultRolesAsync(tenantId, userId, now, operations, cancellationToken);
        if (rolesCreated > 0)
        {
            operations.Add($"Created {rolesCreated} default roles");
        }

        var rolePermissionsCreated = await EnsureDefaultRolePermissionsAsync(tenantId, operations, cancellationToken);
        if (rolePermissionsCreated > 0)
        {
            operations.Add($"Ensured default role permissions ({rolePermissionsCreated})");
        }

        var platformAdminPermissionsCreated = await EnsureGlobalPlatformAdminPermissionsAsync(userId, now, cancellationToken);
        if (platformAdminPermissionsCreated > 0)
        {
            operations.Add($"Ensured PlatformAdmin role permissions ({platformAdminPermissionsCreated})");
        }

        await _auditLogWriter.LogAsync(
            AuditEventNames.PermissionsSeeded,
            "PermissionSeed",
            tenantId,
            tenantId,
            userId,
            _correlationContext.CorrelationId,
            System.Text.Json.JsonSerializer.Serialize(new { tenantId, operations }),
            cancellationToken);

        return new PermissionSeedResult(tenantId, now, operations);
    }

    private async Task EnsureSeedPermissionAsync(CancellationToken cancellationToken)
    {
        var userId = CurrentUserProvider.GetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        var canWritePermissions = await PermissionService.HasPermissionAsync(userId.Value, "Permissions.Write", cancellationToken);
        var canWriteTenants = await PermissionService.HasPermissionAsync(userId.Value, "Tenants.Write", cancellationToken);
        if (!canWritePermissions && !canWriteTenants)
        {
            throw new InvalidOperationException("Permission Permissions.Write or Tenants.Write is required.");
        }
    }

    private async Task<int> EnsureDefaultRolesAsync(
        Guid tenantId,
        Guid? userId,
        DateTime now,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var existingRoleNames = await _dbContext.Roles
            .Where(r => r.TenantId == tenantId)
            .Select(r => r.Name)
            .ToListAsync(cancellationToken);

        var existingRoleSet = new HashSet<string>(existingRoleNames, StringComparer.OrdinalIgnoreCase);
        var defaultRoles = new List<Role>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "TenantAdmin",
                CreatedAt = now,
                CreatedBy = userId
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Operations",
                CreatedAt = now,
                CreatedBy = userId
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "ReadOnly",
                CreatedAt = now,
                CreatedBy = userId
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "PersonalUser",
                CreatedAt = now,
                CreatedBy = userId
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Compliance",
                CreatedAt = now,
                CreatedBy = userId
            }
        };

        var newRoles = defaultRoles
            .Where(role => !existingRoleSet.Contains(role.Name))
            .ToList();

        if (newRoles.Count == 0)
        {
            operations.Add("Default roles already exist - skipped");
            return 0;
        }

        _dbContext.Roles.AddRange(newRoles);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return newRoles.Count;
    }

    private async Task<int> EnsureDefaultRolePermissionsAsync(
        Guid tenantId,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var rolePermissions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["TenantAdmin"] =
            [
                "Users.Read",
                "Users.Invite",
                "Users.Manage",
                "Users.Deactivate",
                "UserInfo.Read",
                "UserInfo.Update",
                "Roles.Read",
                "Roles.Create",
                "Roles.Update",
                "Roles.Delete",
                "Permissions.Read",
                "Settings.Read",
                "Settings.Write",
                "Ledger.Read",
                "Ledger.Write",
                "Ledger.Reconcile",
                "Payment.Read",
                "Payment.Create",
                "Payment.Capture",
                "Payment.Cancel",
                "Payment.Refund",
                "Invoice.Read",
                "Invoice.Create",
                "Invoice.Update",
                "Invoice.Delete",
                "Invoice.Issue",
                "Catalog.Read",
                "Customers.Read",
                "Customers.Create"
            ],
            ["Operations"] =
            [
                "Ledger.Read",
                "Ledger.Write",
                "Ledger.Reconcile",
                "Payment.Read",
                "Payment.Create",
                "Payment.Capture",
                "Payment.Cancel",
                "Payment.Refund",
                "Invoice.Read",
                "Invoice.Create",
                "Invoice.Update",
                "Invoice.Delete",
                "Invoice.Issue",
                "Catalog.Read",
                "Customers.Read",
                "Customers.Create"
            ],
            ["ReadOnly"] =
            [
                "Users.Read",
                "UserInfo.Read",
                "Roles.Read",
                "Settings.Read",
                "Ledger.Read",
                "Payment.Read",
                "Invoice.Read",
                "Catalog.Read",
                "Customers.Read"
            ],
            ["Compliance"] =
            [
                "Users.Read",
                "Settings.Read",
                "Ledger.Read",
                "Payment.Read",
                "Invoice.Read",
                "Catalog.Read",
                "Customers.Read"
            ],
            ["PersonalUser"] =
            [
                "UserInfo.Read",
                "UserInfo.Update",
                "Settings.Read",
                "Settings.Write",
                "Catalog.Read"
            ]
        };

        var roles = await _dbContext.Roles
            .Where(role => role.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var permissions = await _dbContext.Permissions
            .ToListAsync(cancellationToken);

        var permissionLookup = permissions.ToDictionary(permission => permission.Key, StringComparer.OrdinalIgnoreCase);
        var roleLookup = roles.ToDictionary(role => role.Name, StringComparer.OrdinalIgnoreCase);
        var created = 0;

        foreach (var mapping in rolePermissions)
        {
            if (!roleLookup.TryGetValue(mapping.Key, out var role))
            {
                operations.Add($"Role {mapping.Key} not found for permission assignment");
                continue;
            }

            var permissionIds = mapping.Value
                .Where(permissionLookup.ContainsKey)
                .Select(permissionKey => permissionLookup[permissionKey].Id)
                .ToList();

            if (permissionIds.Count == 0)
            {
                continue;
            }

            var existingIds = await _dbContext.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .Select(rp => rp.PermissionId)
                .ToListAsync(cancellationToken);

            var newRolePermissions = permissionIds
                .Where(permissionId => !existingIds.Contains(permissionId))
                .Select(permissionId => new RolePermission
                {
                    Id = Guid.NewGuid(),
                    RoleId = role.Id,
                    PermissionId = permissionId
                })
                .ToList();

            if (newRolePermissions.Count == 0)
            {
                continue;
            }

            _dbContext.RolePermissions.AddRange(newRolePermissions);
            await _dbContext.SaveChangesAsync(cancellationToken);
            created += newRolePermissions.Count;
        }

        return created;
    }

    private async Task<int> EnsureGlobalPlatformAdminPermissionsAsync(
        Guid? userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var platformAdminRole = await _dbContext.Roles
            .FirstOrDefaultAsync(role => role.TenantId == Guid.Empty && role.Name == "PlatformAdmin", cancellationToken);

        if (platformAdminRole == null)
        {
            platformAdminRole = new Role
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.Empty,
                Name = "PlatformAdmin",
                CreatedAt = now,
                CreatedBy = userId
            };

            _dbContext.Roles.Add(platformAdminRole);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var permissionKeys = new[]
        {
            "Tenants.Read",
            "Tenants.Write",
            "Users.Read",
            "Users.Invite",
            "Users.Manage",
            "Users.Deactivate",
            "UserInfo.Read",
            "UserInfo.Update",
            "Settings.Read",
            "Settings.Write",
            "Roles.Read",
            "Roles.Create",
            "Roles.Update",
            "Roles.Delete",
            "Permissions.Read",
            "Permissions.Write",
            "Ledger.Read",
            "Ledger.Write",
            "Ledger.Reconcile",
            "Payment.Read",
            "Payment.Create",
            "Payment.Capture",
            "Payment.Cancel",
            "Payment.Refund",
            "Invoice.Read",
            "Invoice.Create",
            "Invoice.Update",
            "Invoice.Delete",
            "Invoice.Issue",
            "Catalog.Read",
            "Customers.Read",
            "Customers.Create"
        };

        var permissions = await _dbContext.Permissions
            .Where(p => permissionKeys.Contains(p.Key))
            .ToListAsync(cancellationToken);

        if (permissions.Count == 0)
        {
            return 0;
        }

        var existingPermissionIds = await _dbContext.RolePermissions
            .Where(rp => rp.RoleId == platformAdminRole.Id)
            .Select(rp => rp.PermissionId)
            .ToListAsync(cancellationToken);

        var newRolePermissions = permissions
            .Where(permission => !existingPermissionIds.Contains(permission.Id))
            .Select(permission => new RolePermission
            {
                Id = Guid.NewGuid(),
                RoleId = platformAdminRole.Id,
                PermissionId = permission.Id
            })
            .ToList();

        if (newRolePermissions.Count == 0)
        {
            return 0;
        }

        _dbContext.RolePermissions.AddRange(newRolePermissions);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return newRolePermissions.Count;
    }
}
