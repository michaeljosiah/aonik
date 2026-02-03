using Microsoft.EntityFrameworkCore;

using Aonik.Application.Abstractions.Observability;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Identity;
using Aonik.Application.Services;
using Aonik.Application.Services.Compliance;
using Aonik.Application.Services.Identity;
using Aonik.Domain.Ai.Entities;
using Aonik.Domain.Identity.Entities;
using Aonik.Domain.Ledger.Entities;
using Aonik.Domain.Pricing.Entities;
using Aonik.SharedKernel.Abstractions;
using LedgerEntity = Aonik.Domain.Ledger.Entities.Ledger;

namespace Aonik.Application.Services.Identity.Provisioning;

public class TenantProvisioner : AdminServiceBase, ITenantProvisioner, IBootstrapTenantProvisioner
{
    private readonly IAonikDbContext _dbContext;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IClock _clock;
    private readonly ICorrelationContext _correlationContext;

    public TenantProvisioner(
        IAonikDbContext dbContext,
        IAuditLogWriter auditLogWriter,
        IClock clock,
        ICurrentUserProvider currentUserProvider,
        ICorrelationContext correlationContext,
        IPermissionService permissionService)
        : base(currentUserProvider, permissionService)
    {
        _dbContext = dbContext;
        _auditLogWriter = auditLogWriter;
        _clock = clock;
        _correlationContext = correlationContext;
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

        // Check if already provisioned
        var existingLedger = await _dbContext.Ledgers
            .FirstOrDefaultAsync(l => l.TenantId == tenantId, cancellationToken);

        var ledgerCreated = false;
        var chartOfAccountsCount = 0;

        if (existingLedger == null)
        {
            // Create Ledger
            var ledgerId = Guid.NewGuid();
            var ledger = new LedgerEntity
            {
                Id = ledgerId,
                TenantId = tenantId,
                BaseCurrency = tenant.DefaultCurrency,
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.Ledgers.Add(ledger);
            await _dbContext.SaveChangesAsync(cancellationToken);

            ledgerCreated = true;
            actionsPerformed.Add($"Created ledger {ledger.Id} with base currency {tenant.DefaultCurrency}");


            // Create Chart of Accounts
            var accounts = CreateDefaultChartOfAccounts(tenantId, ledgerId, userId, now);
            _dbContext.LedgerAccounts.AddRange(accounts);
            await _dbContext.SaveChangesAsync(cancellationToken);

            chartOfAccountsCount = accounts.Count;
            actionsPerformed.Add($"Created {chartOfAccountsCount} default accounts");
        }
        else
        {
            actionsPerformed.Add("Ledger already exists - skipped");
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

        // Provision AI Route Policy (placeholder)

        var policiesCreated = await ProvisionAiPoliciesAsync(tenantId, userId, now, actionsPerformed, cancellationToken);

        // Provision Fee and Limits Policies (placeholder)
        await ProvisionPricingPoliciesAsync(tenantId, userId, now, actionsPerformed, cancellationToken);

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

        // Check ledger exists
        var hasLedger = await _dbContext.Ledgers
            .AnyAsync(l => l.TenantId == tenantId, cancellationToken);

        if (!hasLedger)
            issues.Add("Tenant does not have a ledger");

        // Check chart of accounts
        var hasChartOfAccounts = await _dbContext.LedgerAccounts
            .AnyAsync(a => a.TenantId == tenantId, cancellationToken);

        if (!hasChartOfAccounts)
            issues.Add("Tenant does not have any ledger accounts");

        // Check roles exist
        var hasRoles = await _dbContext.Roles
            .AnyAsync(r => r.TenantId == tenantId, cancellationToken);

        if (!hasRoles)
            issues.Add("Tenant does not have any roles");

        var isHealthy = issues.Count == 0;

        return new TenantHealthResult(
            isHealthy,
            hasLedger,
            hasRoles,
            hasChartOfAccounts,
            issues
        );
    }

    private static List<LedgerAccount> CreateDefaultChartOfAccounts(Guid tenantId, Guid ledgerId, Guid? userId, DateTime now)
    {
        var accounts = new List<LedgerAccount>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LedgerId = ledgerId,
                AccountType = "Asset",
                Name = "Cash",
                Code = "1000",
                DimensionsJson = "{}",
                CreatedAt = now,
                CreatedBy = userId
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LedgerId = ledgerId,
                AccountType = "Asset",
                Name = "Accounts Receivable",
                Code = "1100",
                DimensionsJson = "{}",
                CreatedAt = now,
                CreatedBy = userId
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LedgerId = ledgerId,
                AccountType = "Liability",
                Name = "Accounts Payable",
                Code = "2000",
                DimensionsJson = "{}",
                CreatedAt = now,
                CreatedBy = userId
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LedgerId = ledgerId,
                AccountType = "Equity",
                Name = "Retained Earnings",
                Code = "3000",
                DimensionsJson = "{}",
                CreatedAt = now,
                CreatedBy = userId
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LedgerId = ledgerId,
                AccountType = "Revenue",
                Name = "Operating Revenue",
                Code = "4000",
                DimensionsJson = "{}",
                CreatedAt = now,
                CreatedBy = userId
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LedgerId = ledgerId,
                AccountType = "Expense",
                Name = "Operating Expenses",
                Code = "5000",
                DimensionsJson = "{}",
                CreatedAt = now,
                CreatedBy = userId
            }
        };

        return accounts;
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

    private async Task<int> ProvisionAiPoliciesAsync(Guid tenantId, Guid? userId, DateTime now, List<string> actionsPerformed, CancellationToken cancellationToken)

    {
        var existingPolicies = await _dbContext.AiRoutePolicies
            .Where(p => p.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        if (existingPolicies.Any())
        {
            actionsPerformed.Add("AI policies already exist - skipped");
            return 0;
        }

        var defaultPolicy = new AiRoutePolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UseCase = "Default",
            RiskTier = "Low",
            DataSensitivity = "Public",
            CostCeiling = 1000.00m,
            PrimaryModelId = Guid.Empty, // TODO: Set actual model ID
            FallbackModelIdsJson = "[]",
            IsActive = true,
            CreatedAt = now,
            CreatedBy = userId
        };

        _dbContext.AiRoutePolicies.Add(defaultPolicy);
        await _dbContext.SaveChangesAsync(cancellationToken);

        actionsPerformed.Add("Created default AI route policy");
        return 1;
    }

    private async Task ProvisionPricingPoliciesAsync(Guid tenantId, Guid? userId, DateTime now, List<string> actionsPerformed, CancellationToken cancellationToken)
    {
        var existingFeePolicies = await _dbContext.FeePolicies
            .Where(p => p.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        if (!existingFeePolicies.Any())
        {
            var feePolicy = new FeePolicy
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Default Fee Policy",
                FixedFee = 0.00m,
                PercentageFee = 0.00m,
                ConditionsJson = "{}",
                IsActive = true,
                CreatedAt = now,
                CreatedBy = userId
            };

            _dbContext.FeePolicies.Add(feePolicy);
            await _dbContext.SaveChangesAsync(cancellationToken);
            actionsPerformed.Add("Created default fee policy");
        }
        else
        {
            actionsPerformed.Add("Fee policies already exist - skipped");
        }

        var existingLimitsPolicies = await _dbContext.LimitsPolicies
            .Where(p => p.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        if (!existingLimitsPolicies.Any())
        {
            var limitsPolicy = new LimitsPolicy
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ScopeType = "Tenant",
                ScopeId = tenantId,
                MaxAmount = 100000.00m,
                Period = "Monthly",
                Currency = "USD",
                IsActive = true,
                CreatedAt = now,
                CreatedBy = userId
            };

            _dbContext.LimitsPolicies.Add(limitsPolicy);
            await _dbContext.SaveChangesAsync(cancellationToken);
            actionsPerformed.Add("Created default limits policy");
        }
        else
        {
            actionsPerformed.Add("Limits policies already exist - skipped");
        }
    }
}
