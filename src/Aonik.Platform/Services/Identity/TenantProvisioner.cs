using Microsoft.EntityFrameworkCore;

using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.Platform.Persistence;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Services;
using Aonik.Platform.Services.Compliance;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Entities.Identity;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Services.Identity;

internal class TenantProvisioner : AdminServiceBase, ITenantProvisioner, IBootstrapTenantProvisioner
{
    private readonly PlatformDbContext _dbContext;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IClock _clock;
    private readonly ICorrelationContext _correlationContext;
    private readonly IEnumerable<ITenantProvisioningContributor> _contributors;

    public TenantProvisioner(
        PlatformDbContext dbContext,
        IAuditLogWriter auditLogWriter,
        IClock clock,
        ICurrentUserProvider currentUserProvider,
        ICorrelationContext correlationContext,
        IPermissionService permissionService,
        IEnumerable<ITenantProvisioningContributor> contributors)
        : base(currentUserProvider, permissionService)
    {
        _dbContext = dbContext;
        _auditLogWriter = auditLogWriter;
        _clock = clock;
        _correlationContext = correlationContext;
        _contributors = contributors;
    }

    public async Task<ProvisionTenantResult> ProvisionTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Tenants.Write", cancellationToken);
        return await ProvisionTenantCoreAsync(tenantId, cancellationToken);
    }

    Task<ProvisionTenantResult> IBootstrapTenantProvisioner.ProvisionTenantAsync(Guid tenantId, CancellationToken cancellationToken)
        => ProvisionTenantCoreAsync(tenantId, cancellationToken);

    private async Task<ProvisionTenantResult> ProvisionTenantCoreAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);


        if (tenant == null)
            throw new InvalidOperationException($"Tenant {tenantId} not found");

        var actionsPerformed = new List<string>();
        var userId = CurrentUserProvider.GetCurrentUserId();
        var now = _clock.UtcNow;

        // Delegate to module contributors (Finance creates Ledger/Accounts/Pricing, AI creates policies)
        var context = new TenantProvisioningContext(tenantId, tenant.DefaultCurrency, userId, now);
        var ledgerCreated = false;
        var chartOfAccountsCount = 0;
        var policiesCreated = 0;

        foreach (var contributor in _contributors)
        {
            var contribution = await contributor.ContributeProvisioningAsync(context, cancellationToken);
            actionsPerformed.AddRange(contribution.ActionsPerformed);

            if (contribution.LedgerCreated)
                ledgerCreated = true;
            chartOfAccountsCount += contribution.ChartOfAccountsCount;
            policiesCreated += contribution.PoliciesCreated;
        }

        // Provision Roles
        var rolesCreated = await ProvisionRolesAsync(tenantId, userId, now, actionsPerformed, cancellationToken);
        var rolePermissionsCreated = await EnsureDefaultRolePermissionsAsync(tenantId, actionsPerformed, cancellationToken);
        var globalPermissionsCreated = await EnsureGlobalPlatformAdminAsync(userId, now, cancellationToken);
        if (globalPermissionsCreated > 0)
        {
            actionsPerformed.Add($"Ensured PlatformAdmin role permissions ({globalPermissionsCreated})");
        }
        if (rolePermissionsCreated > 0)
        {
            actionsPerformed.Add($"Ensured default role permissions ({rolePermissionsCreated})");
        }

        // Log provisioning completion
        await _auditLogWriter.LogAsync(
            AuditEventNames.TenantProvisioned,
            "Tenant",
            tenant.Id,
            tenantId,
            userId,
            _correlationContext.CorrelationId,
            System.Text.Json.JsonSerializer.Serialize(new { tenantId, actionsPerformed }),
            cancellationToken);

        return new ProvisionTenantResult(
            ledgerCreated,
            chartOfAccountsCount,
            rolesCreated,
            policiesCreated,
            actionsPerformed
        );
    }

    public async Task<TenantHealthResult> CheckTenantHealthAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Tenants.Read", cancellationToken);
        var issues = new List<string>();

        // Delegate module-specific health checks to contributors
        foreach (var contributor in _contributors)
        {
            await contributor.ContributeHealthCheckAsync(tenantId, issues, cancellationToken);
        }

        // Check roles exist (Platform-owned)
        var hasRoles = await _dbContext.Roles
            .AnyAsync(r => r.TenantId == tenantId, cancellationToken);

        if (!hasRoles)
            issues.Add("Tenant does not have any roles");

        // Derive health flags from issues
        var hasLedger = !issues.Any(i => i.Contains("ledger", StringComparison.OrdinalIgnoreCase));
        var hasChartOfAccounts = !issues.Any(i => i.Contains("ledger accounts", StringComparison.OrdinalIgnoreCase));
        var isHealthy = issues.Count == 0;

        return new TenantHealthResult(
            isHealthy,
            hasLedger,
            hasRoles,
            hasChartOfAccounts,
            issues
        );
    }

    private async Task<int> ProvisionRolesAsync(Guid tenantId, Guid? userId, DateTime now, List<string> actionsPerformed, CancellationToken cancellationToken)
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
            actionsPerformed.Add("Default roles already exist - skipped");
            return 0;
        }

        _dbContext.Roles.AddRange(newRoles);
        await _dbContext.SaveChangesAsync(cancellationToken);

        actionsPerformed.Add($"Created {newRoles.Count} default roles");
        return newRoles.Count;
    }

    private async Task<int> EnsureDefaultRolePermissionsAsync(
        Guid tenantId,
        List<string> actionsPerformed,
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
                "Catalog.Read"
                ,
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
                "Catalog.Read"
                ,
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
                "Catalog.Read"
                ,
                "Customers.Read"
            ],
            ["Compliance"] =
            [
                "Users.Read",
                "Settings.Read",
                "Ledger.Read",
                "Payment.Read",
                "Invoice.Read",
                "Catalog.Read"
                ,
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
                actionsPerformed.Add($"Role {mapping.Key} not found for permission assignment");
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

    private async Task<int> EnsureGlobalPlatformAdminAsync(Guid? userId, DateTime now, CancellationToken cancellationToken)
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
