using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Identity;
using Aonik.Application.Services.Compliance;
using Aonik.Domain.Ai.Entities;
using Aonik.Domain.Identity.Entities;
using Aonik.Domain.Ledger.Entities;
using Aonik.Domain.Pricing.Entities;
using Aonik.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using LedgerEntity = Aonik.Domain.Ledger.Entities.Ledger;

namespace Aonik.Application.Services.Identity.Provisioning;

public class TenantProvisioner : ITenantProvisioner
{
    private readonly IAonikDbContext _dbContext;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;

    public TenantProvisioner(
        IAonikDbContext dbContext,
        IAuditLogWriter auditLogWriter,
        IClock clock,
        ICurrentUserProvider currentUserProvider)
    {
        _dbContext = dbContext;
        _auditLogWriter = auditLogWriter;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<ProvisionTenantResult> ProvisionTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.TenantId == tenantId, cancellationToken);

        if (tenant == null)
            throw new InvalidOperationException($"Tenant {tenantId} not found");

        var actionsPerformed = new List<string>();
        var userId = _currentUserProvider.GetCurrentUserId();
        var now = _clock.UtcNow;

        // Check if already provisioned
        var existingLedger = await _dbContext.Ledgers
            .FirstOrDefaultAsync(l => l.TenantId == tenantId, cancellationToken);

        var ledgerCreated = false;
        var chartOfAccountsCount = 0;

        if (existingLedger == null)
        {
            // Create Ledger
            var ledger = new LedgerEntity
            {
                LedgerId = Guid.NewGuid(),
                TenantId = tenantId,
                BaseCurrency = tenant.DefaultCurrency,
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.Ledgers.Add(ledger);
            await _dbContext.SaveChangesAsync(cancellationToken);

            ledgerCreated = true;
            actionsPerformed.Add($"Created ledger {ledger.LedgerId} with base currency {tenant.DefaultCurrency}");

            // Create Chart of Accounts
            var accounts = CreateDefaultChartOfAccounts(tenantId, ledger.LedgerId, userId, now);
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

        // Provision AI Route Policy (placeholder)
        var policiesCreated = await ProvisionAiPoliciesAsync(tenantId, userId, now, actionsPerformed, cancellationToken);

        // Provision Fee and Limits Policies (placeholder)
        await ProvisionPricingPoliciesAsync(tenantId, userId, now, actionsPerformed, cancellationToken);

        // Log provisioning completion
        await _auditLogWriter.LogAsync(
            "TenantProvisioned",
            "Tenant",
            tenant.Id,
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
                LedgerAccountId = Guid.NewGuid(),
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
                LedgerAccountId = Guid.NewGuid(),
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
                LedgerAccountId = Guid.NewGuid(),
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
                LedgerAccountId = Guid.NewGuid(),
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
                LedgerAccountId = Guid.NewGuid(),
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
                LedgerAccountId = Guid.NewGuid(),
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
        var existingRoles = await _dbContext.Roles
            .Where(r => r.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        if (existingRoles.Any())
        {
            actionsPerformed.Add($"Roles already exist ({existingRoles.Count}) - skipped");
            return 0;
        }

        var defaultRoles = new List<Role>
        {
            new()
            {
                RoleId = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "TenantAdmin",
                CreatedAt = now,
                CreatedBy = userId
            },
            new()
            {
                RoleId = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Operations",
                CreatedAt = now,
                CreatedBy = userId
            },
            new()
            {
                RoleId = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "ReadOnly",
                CreatedAt = now,
                CreatedBy = userId
            },
            new()
            {
                RoleId = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Compliance",
                CreatedAt = now,
                CreatedBy = userId
            }
        };

        _dbContext.Roles.AddRange(defaultRoles);
        await _dbContext.SaveChangesAsync(cancellationToken);

        actionsPerformed.Add($"Created {defaultRoles.Count} default roles");
        return defaultRoles.Count;
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
            AiRoutePolicyId = Guid.NewGuid(),
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
                FeePolicyId = Guid.NewGuid(),
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
                LimitsPolicyId = Guid.NewGuid(),
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
