using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Entities.Pricing;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ledgers;
using Microsoft.EntityFrameworkCore;
using LedgerEntity = Aonik.Finance.Entities.Ledger.Ledger;

namespace Aonik.Finance.Services.Provisioning;

/// <summary>
/// Finance module's contribution to tenant provisioning.
/// Creates Ledger, chart of accounts, fee policy, and limits policy.
/// </summary>
internal class FinanceTenantProvisioningContributor : ITenantProvisioningContributor
{
    private readonly FinanceDbContext _dbContext;
    private readonly IReadOnlyList<ILedgerAccountContributor> _accountContributors;

    public FinanceTenantProvisioningContributor(
        FinanceDbContext dbContext,
        IEnumerable<ILedgerAccountContributor>? accountContributors = null)
    {
        _dbContext = dbContext;
        // Spec 088 §5 — modules declare the accounts they post to; Finance still owns the chart.
        // Optional so existing fixtures that construct this with just a DbContext keep compiling.
        _accountContributors = accountContributors?.ToList() ?? [];
    }

    public string ModuleName => "Finance";

    public async Task<TenantProvisioningContribution> ContributeProvisioningAsync(
        TenantProvisioningContext context,
        CancellationToken cancellationToken = default)
    {
        var actions = new List<string>();
        var ledgerCreated = false;
        var chartOfAccountsCount = 0;
        var policiesCreated = 0;

        // Create Ledger + Chart of Accounts
        var existingLedger = await _dbContext.Ledgers
            .FirstOrDefaultAsync(l => l.TenantId == context.TenantId, cancellationToken);

        if (existingLedger == null)
        {
            var ledgerId = Guid.NewGuid();
            var ledger = new LedgerEntity
            {
                Id = ledgerId,
                TenantId = context.TenantId,
                BaseCurrency = context.DefaultCurrency,
                CreatedAt = context.Now,
                CreatedBy = context.UserId
            };
            _dbContext.Ledgers.Add(ledger);
            await _dbContext.SaveChangesAsync(cancellationToken);

            ledgerCreated = true;
            actions.Add($"Created ledger {ledger.Id} with base currency {context.DefaultCurrency}");

            var accounts = CreateDefaultChartOfAccounts(context.TenantId, ledgerId, context.UserId, context.Now);
            _dbContext.LedgerAccounts.AddRange(accounts);
            await _dbContext.SaveChangesAsync(cancellationToken);

            chartOfAccountsCount = accounts.Count;
            actions.Add($"Created {chartOfAccountsCount} default accounts");
        }
        else
        {
            actions.Add("Ledger already exists - skipped");
        }

        // Provision Fee Policy
        var existingFeePolicies = await _dbContext.FeePolicies
            .Where(p => p.TenantId == context.TenantId)
            .ToListAsync(cancellationToken);

        if (!existingFeePolicies.Any())
        {
            var feePolicy = new FeePolicy
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                Name = "Default Fee Policy",
                FixedFee = 0.00m,
                PercentageFee = 0.00m,
                ConditionsJson = "{}",
                IsActive = true,
                CreatedAt = context.Now,
                CreatedBy = context.UserId
            };

            _dbContext.FeePolicies.Add(feePolicy);
            await _dbContext.SaveChangesAsync(cancellationToken);
            actions.Add("Created default fee policy");
            policiesCreated++;
        }
        else
        {
            actions.Add("Fee policies already exist - skipped");
        }

        // Provision Limits Policy
        var existingLimitsPolicies = await _dbContext.LimitsPolicies
            .Where(p => p.TenantId == context.TenantId)
            .ToListAsync(cancellationToken);

        if (!existingLimitsPolicies.Any())
        {
            var limitsPolicy = new LimitsPolicy
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                ScopeType = "Tenant",
                ScopeId = context.TenantId,
                MaxAmount = 100000.00m,
                Period = "Monthly",
                Currency = "USD",
                IsActive = true,
                CreatedAt = context.Now,
                CreatedBy = context.UserId
            };

            _dbContext.LimitsPolicies.Add(limitsPolicy);
            await _dbContext.SaveChangesAsync(cancellationToken);
            actions.Add("Created default limits policy");
            policiesCreated++;
        }
        else
        {
            actions.Add("Limits policies already exist - skipped");
        }

        chartOfAccountsCount += await EnsureContributedAccountsAsync(context, actions, cancellationToken);

        return new TenantProvisioningContribution(actions, ledgerCreated, chartOfAccountsCount, policiesCreated);
    }

    public async Task ContributeHealthCheckAsync(
        Guid tenantId,
        List<string> issues,
        CancellationToken cancellationToken = default)
    {
        var hasLedger = await _dbContext.Ledgers
            .AnyAsync(l => l.TenantId == tenantId, cancellationToken);

        if (!hasLedger)
            issues.Add("Tenant does not have a ledger");

        var hasChartOfAccounts = await _dbContext.LedgerAccounts
            .AnyAsync(a => a.TenantId == tenantId, cancellationToken);

        if (!hasChartOfAccounts)
            issues.Add("Tenant does not have any ledger accounts");
    }

    private static List<LedgerAccount> CreateDefaultChartOfAccounts(Guid tenantId, Guid ledgerId, Guid? userId, DateTime now)
    {
        return
        [
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
                // Suspense account that absorbs the captured-cash leg until the
                // matching invoice is settled. Payment capture credits it; invoice
                // settlement debits it back to revenue, so it nets to zero per
                // funded order. Resolved by code 2100 in LedgerPostingService.
                AccountType = "Liability",
                Name = "Payments Clearing",
                Code = "2100",
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
        ];
    }

    /// <summary>
    /// Creates the accounts other modules declare (Spec 088 §5). Idempotent and additive: an
    /// existing code is left exactly as it is — never renamed or retyped — because an operator may
    /// have adjusted it deliberately, and silently rewriting an account a ledger already posts to
    /// would rewrite history rather than correct it.
    /// </summary>
    private async Task<int> EnsureContributedAccountsAsync(
        TenantProvisioningContext context,
        List<string> actions,
        CancellationToken cancellationToken)
    {
        if (_accountContributors.Count == 0)
            return 0;

        var ledger = await _dbContext.Ledgers
            .FirstOrDefaultAsync(l => l.TenantId == context.TenantId, cancellationToken);

        if (ledger is null)
            return 0;

        var existingCodes = await _dbContext.LedgerAccounts
            .Where(a => a.TenantId == context.TenantId && a.LedgerId == ledger.Id)
            .Select(a => a.Code)
            .ToListAsync(cancellationToken);

        var known = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);
        var created = 0;

        foreach (var contributor in _accountContributors)
        {
            foreach (var account in contributor.GetAccounts())
            {
                if (!known.Add(account.Code))
                    continue;

                _dbContext.LedgerAccounts.Add(new LedgerAccount
                {
                    Id = Guid.NewGuid(),
                    TenantId = context.TenantId,
                    LedgerId = ledger.Id,
                    Code = account.Code,
                    Name = account.Name,
                    AccountType = account.AccountType,
                    DimensionsJson = "{}",
                    CreatedAt = context.Now,
                    CreatedBy = context.UserId
                });

                created++;
            }

            if (created > 0)
                actions.Add($"Created ledger accounts contributed by {contributor.ModuleName}");
        }

        if (created > 0)
            await _dbContext.SaveChangesAsync(cancellationToken);

        return created;
    }
}
