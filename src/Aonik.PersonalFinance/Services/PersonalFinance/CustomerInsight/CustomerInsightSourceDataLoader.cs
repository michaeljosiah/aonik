using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions.Ordering;
using Microsoft.EntityFrameworkCore;

namespace Aonik.PersonalFinance.Services.CustomerInsight;

/// <summary>
/// Encapsulates every read against <see cref="PersonalFinanceDbContext"/> the snapshot
/// generator needs (accounts, transactions, bills, subscriptions, recurring bills,
/// debt repayments, budgets, goals, profile, orders, household). Each loader records
/// availability or failure on the supplied <see cref="CustomerInsightCoverageAccumulator"/>
/// so the snapshot can correctly report partial coverage.
///
/// Order history is consumed via <see cref="ICustomerOrderHistoryReader"/> (SharedKernel
/// contract) rather than direct <c>Finance.Entities.Orders</c> access so this loader
/// lives in PersonalFinance.
/// </summary>
internal sealed class CustomerInsightSourceDataLoader
{
    private readonly PersonalFinanceDbContext _dbContext;
    private readonly ICustomerOrderHistoryReader _orderHistoryReader;

    public CustomerInsightSourceDataLoader(
        PersonalFinanceDbContext dbContext,
        ICustomerOrderHistoryReader orderHistoryReader)
    {
        _dbContext = dbContext;
        _orderHistoryReader = orderHistoryReader;
    }

    public async Task<List<PersonalAccount>> LoadAccountsAsync(
        Guid tenantId,
        Guid userId,
        CustomerInsightCoverageAccumulator coverageAccumulator,
        CancellationToken cancellationToken)
    {
        try
        {
            var results = await _dbContext.PersonalAccounts
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.UserId == userId)
                .ToListAsync(cancellationToken);

            coverageAccumulator.MarkAvailable("accounts");

            return results
                .Where(x => !x.IsArchived && !CustomerInsightNormalization.IsArchivedStatus(x.Status))
                .OrderBy(x => CustomerInsightNormalization.NormalizeCurrency(x.Currency))
                .ThenBy(x => CustomerInsightNormalization.NormalizeKey(x.Name))
                .ThenBy(x => x.Id)
                .ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Critical source domain 'accounts' could not be loaded.", ex);
        }
    }

    public async Task<List<PersonalTransaction>> LoadTransactionsAsync(
        Guid tenantId,
        Guid userId,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        CustomerInsightCoverageAccumulator coverageAccumulator,
        CancellationToken cancellationToken)
    {
        try
        {
            var results = await _dbContext.PersonalTransactions
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId
                    && x.UserId == userId
                    && x.OccurredAt >= windowStartUtc
                    && x.OccurredAt <= windowEndUtc)
                .ToListAsync(cancellationToken);

            coverageAccumulator.MarkAvailable("transactions");
            return results;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Critical source domain 'transactions' could not be loaded.", ex);
        }
    }

    public Task<List<Bill>> LoadBillsAsync(
        Guid tenantId,
        Guid userId,
        CustomerInsightCoverageAccumulator coverageAccumulator,
        CancellationToken cancellationToken) =>
        LoadOptionalDomainAsync(
            async ct =>
            {
                var results = await _dbContext.Bills
                    .AsNoTracking()
                    .Where(x => x.TenantId == tenantId && x.UserId == userId)
                    .ToListAsync(ct);

                return results
                    .Where(x => CustomerInsightNormalization.IsActiveStatus(x.Status))
                    .OrderBy(x => x.NextDueDate)
                    .ThenBy(x => CustomerInsightNormalization.NormalizeKey(x.Payee))
                    .ThenBy(x => x.Id)
                    .ToList();
            },
            "bills",
            "metrics.obligations.upcomingBills",
            coverageAccumulator,
            cancellationToken);

    public Task<List<Subscription>> LoadSubscriptionsAsync(
        Guid tenantId,
        Guid userId,
        CustomerInsightCoverageAccumulator coverageAccumulator,
        CancellationToken cancellationToken) =>
        LoadOptionalDomainAsync(
            async ct =>
            {
                var results = await _dbContext.Subscriptions
                    .AsNoTracking()
                    .Where(x => x.TenantId == tenantId && x.UserId == userId)
                    .ToListAsync(ct);

                return results
                    .Where(x => CustomerInsightNormalization.IsActiveStatus(x.Status))
                    .OrderBy(x => x.RenewalDate)
                    .ThenBy(x => CustomerInsightNormalization.NormalizeKey(x.Merchant))
                    .ThenBy(x => x.Id)
                    .ToList();
            },
            "subscriptions",
            "metrics.obligations.subscriptions",
            coverageAccumulator,
            cancellationToken);

    public Task<List<PersonalRecurringBill>> LoadPersonalRecurringBillsAsync(
        Guid tenantId,
        Guid userId,
        CustomerInsightCoverageAccumulator coverageAccumulator,
        CancellationToken cancellationToken) =>
        LoadOptionalDomainAsync(
            async ct =>
            {
                var results = await _dbContext.PersonalRecurringBills
                    .AsNoTracking()
                    .Where(x => x.TenantId == tenantId && x.UserId == userId)
                    .ToListAsync(ct);

                return results
                    .Where(x => CustomerInsightNormalization.IsActiveStatus(x.Status) && x.VerificationStatus != "Rejected")
                    .OrderBy(x => x.NextDueDate)
                    .ThenBy(x => CustomerInsightNormalization.NormalizeKey(x.Payee))
                    .ThenBy(x => x.Id)
                    .ToList();
            },
            "personalRecurringBills",
            "metrics.obligations.personalRecurringBills",
            coverageAccumulator,
            cancellationToken);

    public Task<List<DebtRepayment>> LoadDebtRepaymentsAsync(
        Guid tenantId,
        Guid userId,
        CustomerInsightCoverageAccumulator coverageAccumulator,
        CancellationToken cancellationToken) =>
        LoadOptionalDomainAsync(
            async ct =>
            {
                var results = await _dbContext.DebtRepayments
                    .AsNoTracking()
                    .Where(x => x.TenantId == tenantId && x.UserId == userId)
                    .ToListAsync(ct);

                return results
                    .Where(x => CustomerInsightNormalization.IsActiveStatus(x.Status) && x.VerificationStatus != "Rejected")
                    .OrderBy(x => x.NextDueDate)
                    .ThenBy(x => CustomerInsightNormalization.NormalizeKey(x.CreditorName))
                    .ThenBy(x => x.Id)
                    .ToList();
            },
            "debtRepayments",
            "metrics.obligations.debtRepayments",
            coverageAccumulator,
            cancellationToken);

    public Task<List<Budget>> LoadBudgetsAsync(
        Guid tenantId,
        Guid userId,
        CustomerInsightCoverageAccumulator coverageAccumulator,
        CancellationToken cancellationToken) =>
        LoadOptionalDomainAsync(
            async ct =>
            {
                var results = await _dbContext.Budgets
                    .AsNoTracking()
                    .Include(x => x.Lines)
                    .Where(x => x.TenantId == tenantId && x.UserId == userId)
                    .ToListAsync(ct);

                return results
                    .Where(x => CustomerInsightNormalization.IsActiveStatus(x.Status))
                    .OrderByDescending(x => x.PeriodStart)
                    .ThenBy(x => x.Id)
                    .ToList();
            },
            "budgets",
            "metrics.budgets",
            coverageAccumulator,
            cancellationToken);

    public Task<List<Goal>> LoadGoalsAsync(
        Guid tenantId,
        Guid userId,
        CustomerInsightCoverageAccumulator coverageAccumulator,
        CancellationToken cancellationToken) =>
        LoadOptionalDomainAsync(
            async ct =>
            {
                var results = await _dbContext.Goals
                    .AsNoTracking()
                    .Where(x => x.TenantId == tenantId && x.UserId == userId)
                    .ToListAsync(ct);

                return results
                    .Where(x => CustomerInsightNormalization.IsActiveStatus(x.Status))
                    .OrderBy(x => x.TargetDate ?? DateTime.MaxValue)
                    .ThenBy(x => CustomerInsightNormalization.NormalizeKey(x.Name))
                    .ThenBy(x => x.Id)
                    .ToList();
            },
            "goals",
            "metrics.goals",
            coverageAccumulator,
            cancellationToken);

    public async Task<PersonalProfile?> LoadPersonalProfileAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _dbContext.PersonalProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == userId, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<OrderHistoryItem>> LoadOrdersAsync(
        Guid tenantId,
        Guid partyId,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        CancellationToken cancellationToken)
    {
        return await _orderHistoryReader.GetForPartyAsync(
            tenantId,
            partyId,
            windowStartUtc,
            windowEndUtc,
            cancellationToken);
    }

    public async Task<(Household? household, List<HouseholdMember> members)> LoadHouseholdAsync(
        Guid tenantId,
        Guid householdId,
        CustomerInsightCoverageAccumulator coverageAccumulator,
        CancellationToken cancellationToken)
    {
        try
        {
            var household = await _dbContext.Households
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == householdId, cancellationToken);

            if (household is null)
            {
                return (null, []);
            }

            var members = await _dbContext.HouseholdMembers
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.HouseholdId == householdId)
                .ToListAsync(cancellationToken);

            foreach (var member in members)
            {
                HouseholdMembershipRules.NormalizeLegacyMember(member);
            }

            members = members.Where(HouseholdMembershipRules.IsAccepted).ToList();

            coverageAccumulator.MarkAvailable("household");
            return (household, members);
        }
        catch (Exception ex)
        {
            coverageAccumulator.MarkMissing("household", "householdContext", ex.Message);
            return (null, []);
        }
    }

    public async Task<List<T>> LoadOptionalDomainAsync<T>(
        Func<CancellationToken, Task<List<T>>> loader,
        string domainName,
        string sectionName,
        CustomerInsightCoverageAccumulator coverageAccumulator,
        CancellationToken cancellationToken)
    {
        try
        {
            var results = await loader(cancellationToken);
            coverageAccumulator.MarkAvailable(domainName);
            return results;
        }
        catch (Exception ex)
        {
            coverageAccumulator.MarkMissing(domainName, sectionName, ex.Message);
            return [];
        }
    }

    public async Task<IReadOnlyList<OrderHistoryItem>> LoadOptionalDomainAsync(
        Func<CancellationToken, Task<IReadOnlyList<OrderHistoryItem>>> loader,
        string domainName,
        string sectionName,
        CustomerInsightCoverageAccumulator coverageAccumulator,
        CancellationToken cancellationToken)
    {
        try
        {
            var results = await loader(cancellationToken);
            coverageAccumulator.MarkAvailable(domainName);
            return results;
        }
        catch (Exception ex)
        {
            coverageAccumulator.MarkMissing(domainName, sectionName, ex.Message);
            return [];
        }
    }
}
