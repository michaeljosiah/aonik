using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class CustomerInsightSnapshotGenerator : ICustomerInsightSnapshotGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly HashSet<string> FixedExpenseCategories =
    [
        TransactionCategoryReference.Housing,
        TransactionCategoryReference.Bills,
        TransactionCategoryReference.Subscriptions,
        TransactionCategoryReference.LoanPayments,
        TransactionCategoryReference.BankFees
    ];

    private static readonly HashSet<string> EssentialExpenseCategories =
    [
        TransactionCategoryReference.Housing,
        TransactionCategoryReference.Groceries,
        TransactionCategoryReference.Bills,
        TransactionCategoryReference.Health,
        TransactionCategoryReference.Education,
        TransactionCategoryReference.Transport,
        TransactionCategoryReference.LoanPayments,
        TransactionCategoryReference.FamilySupport
    ];

    private static readonly HashSet<string> SavingsContributionCategories =
    [
        TransactionCategoryReference.Savings,
        TransactionCategoryReference.Investments
    ];

    private static readonly HashSet<string> TransferCategories =
    [
        TransactionCategoryReference.TransferIn,
        TransactionCategoryReference.TransferOut
    ];

    private readonly FinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;

    public CustomerInsightSnapshotGenerator(
        FinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
    }

    public async Task<GeneratedCustomerInsightSnapshot> GenerateAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var asOfUtc = _clock.UtcNow;
        var windowEndUtc = ResolveWindowEnd(asOfUtc);
        var operationalWindowStartUtc = ResolveOperationalWindowStart(asOfUtc);
        var trendWindowStartUtc = ResolveTrendWindowStart(asOfUtc);
        var behaviourWindowStartUtc = ResolveBehaviourWindowStart(asOfUtc);
        var lookaheadEndUtc = ResolveLookaheadEnd(asOfUtc);

        var coverageAccumulator = new CustomerInsightCoverageAccumulator();

        var accounts = await LoadAccountsAsync(tenantId, userId, coverageAccumulator, cancellationToken);
        var transactions = await LoadTransactionsAsync(
            tenantId,
            userId,
            behaviourWindowStartUtc,
            windowEndUtc,
            coverageAccumulator,
            cancellationToken);

        var bills = await LoadOptionalDomainAsync(
            async ct =>
            {
                var results = await _dbContext.Bills
                    .AsNoTracking()
                    .Where(x => x.TenantId == tenantId && x.UserId == userId)
                    .ToListAsync(ct);

                return results
                    .Where(x => IsActiveStatus(x.Status))
                    .OrderBy(x => x.NextDueDate)
                    .ThenBy(x => NormalizeKey(x.Payee))
                    .ThenBy(x => x.Id)
                    .ToList();
            },
            "bills",
            "metrics.obligations.upcomingBills",
            coverageAccumulator,
            cancellationToken);

        var subscriptions = await LoadOptionalDomainAsync(
            async ct =>
            {
                var results = await _dbContext.Subscriptions
                    .AsNoTracking()
                    .Where(x => x.TenantId == tenantId && x.UserId == userId)
                    .ToListAsync(ct);

                return results
                    .Where(x => IsActiveStatus(x.Status))
                    .OrderBy(x => x.RenewalDate)
                    .ThenBy(x => NormalizeKey(x.Merchant))
                    .ThenBy(x => x.Id)
                    .ToList();
            },
            "subscriptions",
            "metrics.obligations.subscriptions",
            coverageAccumulator,
            cancellationToken);

        var personalRecurringBills = await LoadOptionalDomainAsync(
            async ct =>
            {
                var results = await _dbContext.PersonalRecurringBills
                    .AsNoTracking()
                    .Where(x => x.TenantId == tenantId && x.UserId == userId)
                    .ToListAsync(ct);

                return results
                    .Where(x => IsActiveStatus(x.Status) && x.VerificationStatus != "Rejected")
                    .OrderBy(x => x.NextDueDate)
                    .ThenBy(x => NormalizeKey(x.Payee))
                    .ThenBy(x => x.Id)
                    .ToList();
            },
            "personalRecurringBills",
            "metrics.obligations.personalRecurringBills",
            coverageAccumulator,
            cancellationToken);

        var debtRepayments = await LoadOptionalDomainAsync(
            async ct =>
            {
                var results = await _dbContext.DebtRepayments
                    .AsNoTracking()
                    .Where(x => x.TenantId == tenantId && x.UserId == userId)
                    .ToListAsync(ct);

                return results
                    .Where(x => IsActiveStatus(x.Status) && x.VerificationStatus != "Rejected")
                    .OrderBy(x => x.NextDueDate)
                    .ThenBy(x => NormalizeKey(x.CreditorName))
                    .ThenBy(x => x.Id)
                    .ToList();
            },
            "debtRepayments",
            "metrics.obligations.debtRepayments",
            coverageAccumulator,
            cancellationToken);

        var budgets = await LoadOptionalDomainAsync(
            async ct =>
            {
                var results = await _dbContext.Budgets
                    .AsNoTracking()
                    .Include(x => x.Lines)
                    .Where(x => x.TenantId == tenantId && x.UserId == userId)
                    .ToListAsync(ct);

                return results
                    .Where(x => IsActiveStatus(x.Status))
                    .OrderByDescending(x => x.PeriodStart)
                    .ThenBy(x => x.Id)
                    .ToList();
            },
            "budgets",
            "metrics.budgets",
            coverageAccumulator,
            cancellationToken);

        var goals = await LoadOptionalDomainAsync(
            async ct =>
            {
                var results = await _dbContext.Goals
                    .AsNoTracking()
                    .Where(x => x.TenantId == tenantId && x.UserId == userId)
                    .ToListAsync(ct);

                return results
                    .Where(x => IsActiveStatus(x.Status))
                    .OrderBy(x => x.TargetDate ?? DateTime.MaxValue)
                    .ThenBy(x => NormalizeKey(x.Name))
                    .ThenBy(x => x.Id)
                    .ToList();
            },
            "goals",
            "metrics.goals",
            coverageAccumulator,
            cancellationToken);

        var accountNameById = accounts.ToDictionary(
            x => x.Id,
            x => string.IsNullOrWhiteSpace(x.Name) ? "Unnamed account" : x.Name.Trim());

        var normalizedTransactions = transactions
            .Select(x => NormalizeTransaction(x, accountNameById))
            .OrderBy(x => x.OccurredAtUtc)
            .ThenBy(x => x.Currency)
            .ThenBy(x => x.Amount)
            .ThenBy(x => x.Id)
            .ToList();

        var nonTransferTransactions = normalizedTransactions
            .Where(x => !x.IsConfirmedTransfer)
            .ToList();

        var operationalTransactions = FilterTransactions(nonTransferTransactions, operationalWindowStartUtc, windowEndUtc);
        var previousOperationalTransactions = FilterTransactions(
            nonTransferTransactions,
            operationalWindowStartUtc.AddDays(-CustomerInsightSnapshotContract.OperationalWindowDays),
            operationalWindowStartUtc.AddTicks(-1));
        var trendTransactions = FilterTransactions(nonTransferTransactions, trendWindowStartUtc, windowEndUtc);

        var recurringMerchantCandidates = BuildRecurringMerchantCandidates(nonTransferTransactions);
        var upcomingObligationsByCurrency = ComputeUpcomingObligationsByCurrency(asOfUtc, bills, subscriptions, personalRecurringBills, debtRepayments, lookaheadEndUtc);
        var cashPosition = BuildCashPosition(accounts, upcomingObligationsByCurrency);
        var incomeSummary = BuildIncomeSummary(operationalTransactions, previousOperationalTransactions, nonTransferTransactions, operationalWindowStartUtc, windowEndUtc);
        var expenseSummary = BuildExpenseSummary(
            operationalTransactions,
            previousOperationalTransactions,
            trendTransactions,
            accounts,
            recurringMerchantCandidates,
            operationalWindowStartUtc,
            windowEndUtc);
        var categoryInsights = BuildCategoryInsights(operationalTransactions, previousOperationalTransactions, nonTransferTransactions, asOfUtc, operationalWindowStartUtc, windowEndUtc);
        var merchantInsights = BuildMerchantInsights(operationalTransactions, previousOperationalTransactions, nonTransferTransactions, asOfUtc, recurringMerchantCandidates, operationalWindowStartUtc, windowEndUtc);
        var obligationInsights = BuildObligationInsights(asOfUtc, bills, subscriptions, personalRecurringBills, debtRepayments, lookaheadEndUtc, cashPosition);
        var budgetInsights = BuildBudgetInsights(budgets, normalizedTransactions, asOfUtc);
        var goalInsights = BuildGoalInsights(goals, normalizedTransactions, trendWindowStartUtc, windowEndUtc);

        var metrics = new CustomerInsightMetrics(
            cashPosition,
            incomeSummary,
            expenseSummary,
            categoryInsights,
            merchantInsights,
            obligationInsights,
            budgetInsights,
            goalInsights);

        var signals = BuildSignals(
            asOfUtc,
            normalizedTransactions,
            operationalTransactions,
            previousOperationalTransactions,
            cashPosition,
            incomeSummary,
            expenseSummary,
            categoryInsights,
            merchantInsights,
            obligationInsights,
            budgetInsights,
            subscriptions,
            recurringMerchantCandidates);

        var riskOverview = BuildRiskOverview(obligationInsights, budgetInsights, categoryInsights, merchantInsights, signals);

        var evidenceWarnings = coverageAccumulator.Warnings
            .Concat([
                "Transfer exclusion only applies to transactions already normalized as transfers; candidate transfer matching is not stored separately yet."
            ])
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var evidence = new CustomerInsightEvidence(
            normalizedTransactions.Count,
            normalizedTransactions.Count(x => x.IsConfirmedTransfer),
            accounts.Select(x => x.Id).OrderBy(x => x).ToList(),
            behaviourWindowStartUtc,
            windowEndUtc,
            [
                new CustomerInsightSourceCount("accounts", accounts.Count),
                new CustomerInsightSourceCount("transactions", transactions.Count),
                new CustomerInsightSourceCount("bills", bills.Count),
                new CustomerInsightSourceCount("subscriptions", subscriptions.Count),
                new CustomerInsightSourceCount("budgets", budgets.Count),
                new CustomerInsightSourceCount("goals", goals.Count)
            ],
            [
                new CustomerInsightExcludedDataCount(
                    "confirmed_internal_transfers",
                    normalizedTransactions.Count(x => x.IsConfirmedTransfer),
                    "Excluded from canonical inflow and outflow totals.")
            ],
            [
                $"schema:{CustomerInsightSnapshotContract.SchemaVersion}",
                $"generator:{CustomerInsightSnapshotContract.GeneratorVersion}",
                $"transfer_policy:{CustomerInsightSnapshotContract.TransferPolicyNormalizedTransfers}",
                $"monetary_policy:{CustomerInsightSnapshotContract.MonetaryPolicyNativeCurrency}",
                $"operational_window_days:{CustomerInsightSnapshotContract.OperationalWindowDays}",
                $"trend_window_days:{CustomerInsightSnapshotContract.TrendWindowDays}",
                $"behaviour_window_days:{CustomerInsightSnapshotContract.BehaviourWindowDays}",
                $"obligations_lookahead_days:{CustomerInsightSnapshotContract.ObligationsLookaheadDays}",
                $"budget_pressure_threshold_percent:{CustomerInsightSnapshotContract.BudgetPressureThresholdPercent}"
            ],
            evidenceWarnings);

        var coverage = coverageAccumulator.Build();

        var currencies = CollectCurrencies(accounts, normalizedTransactions, bills, subscriptions, budgets, goals);

        var profile = await LoadPersonalProfileAsync(tenantId, userId, cancellationToken);

        var orderHistory = profile is not null
            ? await LoadOptionalDomainAsync(
                ct => LoadOrdersAsync(tenantId, profile.PartyId, behaviourWindowStartUtc, windowEndUtc, ct),
                "orders",
                "orderHistory",
                coverageAccumulator,
                cancellationToken)
            : new List<Order>();

        var (household, householdMembers) = profile?.HouseholdId.HasValue == true
            ? await LoadHouseholdAsync(tenantId, profile.HouseholdId.Value, coverageAccumulator, cancellationToken)
            : (null, new List<HouseholdMember>());

        var orderHistorySection = orderHistory.Count > 0
            ? BuildOrderHistory(orderHistory, behaviourWindowStartUtc, windowEndUtc)
            : null;

        var householdContextSection = household is not null
            ? BuildHouseholdContext(household, householdMembers, userId)
            : null;

        var snapshot = new CustomerInsightSnapshotDocument(
            CustomerInsightSnapshotContract.SchemaVersion,
            userId,
            tenantId,
            asOfUtc,
            new CustomerInsightAnalysisWindow(
                behaviourWindowStartUtc,
                windowEndUtc,
                CustomerInsightSnapshotContract.OperationalWindowDays,
                CustomerInsightSnapshotContract.TrendWindowDays,
                CustomerInsightSnapshotContract.BehaviourWindowDays,
                CustomerInsightSnapshotContract.ObligationsLookaheadDays),
            new CustomerInsightCurrencyPolicy(
                CustomerInsightSnapshotContract.MonetaryPolicyNativeCurrency,
                null,
                null,
                CustomerInsightSnapshotContract.TransferPolicyNormalizedTransfers),
            currencies,
            coverage,
            metrics,
            signals,
            riskOverview,
            evidence,
            orderHistorySection,
            householdContextSection);

        var snapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions);
        var sourceHash = ComputeSourceHash(
            tenantId,
            userId,
            behaviourWindowStartUtc,
            windowEndUtc,
            coverage,
            accounts,
            normalizedTransactions,
            bills,
            subscriptions,
            budgets,
            goals);

        return new GeneratedCustomerInsightSnapshot(
            asOfUtc,
            behaviourWindowStartUtc,
            windowEndUtc,
            sourceHash,
            CustomerInsightSnapshotContract.GeneratorVersion,
            snapshotJson,
            snapshot);
    }

    private async Task<List<PersonalAccount>> LoadAccountsAsync(
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
                .Where(x => !x.IsArchived && !IsArchivedStatus(x.Status))
                .OrderBy(x => NormalizeCurrency(x.Currency))
                .ThenBy(x => NormalizeKey(x.Name))
                .ThenBy(x => x.Id)
                .ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Critical source domain 'accounts' could not be loaded.", ex);
        }
    }

    private async Task<List<PersonalTransaction>> LoadTransactionsAsync(
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

    private static List<NormalizedTransaction> FilterTransactions(
        IEnumerable<NormalizedTransaction> transactions,
        DateTime startUtc,
        DateTime endUtc)
    {
        return transactions
            .Where(x => x.OccurredAtUtc >= startUtc && x.OccurredAtUtc <= endUtc)
            .ToList();
    }

    private async Task<List<T>> LoadOptionalDomainAsync<T>(
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

    private static CustomerInsightCashPosition BuildCashPosition(
        IReadOnlyList<PersonalAccount> accounts,
        IReadOnlyDictionary<string, decimal> upcomingObligationsByCurrency)
    {
        var totalBalanceByCurrency = accounts
            .GroupBy(x => NormalizeCurrency(x.Currency))
            .OrderBy(x => x.Key)
            .Select(x => new CustomerInsightMoneyAmount(x.Key, decimal.Round(x.Sum(y => y.CurrentBalance), 2)))
            .ToList();

        var availableBalanceByCurrency = totalBalanceByCurrency
            .Select(x =>
            {
                var committed = upcomingObligationsByCurrency.TryGetValue(x.Currency, out var amount) ? amount : 0m;
                return new CustomerInsightMoneyAmount(x.Currency, decimal.Round(Math.Max(x.Amount - committed, 0m), 2));
            })
            .ToList();

        var absoluteTotals = accounts
            .GroupBy(x => NormalizeCurrency(x.Currency))
            .ToDictionary(
                x => x.Key,
                x => x.Sum(y => Math.Abs(y.CurrentBalance)),
                StringComparer.Ordinal);

        var balancesByAccount = accounts
            .Select(x =>
            {
                var currency = NormalizeCurrency(x.Currency);
                var absoluteTotal = absoluteTotals.TryGetValue(currency, out var total) ? total : 0m;
                var balanceShare = absoluteTotal <= 0m ? 0m : Math.Abs(x.CurrentBalance) / absoluteTotal * 100m;

                return new CustomerInsightAccountBalance(
                    x.Id,
                    string.IsNullOrWhiteSpace(x.Name) ? "Unnamed account" : x.Name.Trim(),
                    string.IsNullOrWhiteSpace(x.AccountType) ? "Unknown" : x.AccountType.Trim(),
                    currency,
                    decimal.Round(x.CurrentBalance, 2),
                    decimal.Round(balanceShare, 2));
            })
            .OrderBy(x => x.Currency)
            .ThenByDescending(x => Math.Abs(x.CurrentBalance))
            .ThenBy(x => x.AccountId)
            .ToList();

        var concentration = absoluteTotals
            .OrderBy(x => x.Key)
            .Select(x =>
            {
                var highestBalance = accounts
                    .Where(y => NormalizeCurrency(y.Currency) == x.Key)
                    .Select(y => Math.Abs(y.CurrentBalance))
                    .DefaultIfEmpty(0m)
                    .Max();

                var ratio = x.Value <= 0m ? 0m : highestBalance / x.Value * 100m;
                return new CustomerInsightConcentrationRatio(x.Key, decimal.Round(ratio, 2));
            })
            .ToList();

        return new CustomerInsightCashPosition(
            accounts.Count,
            totalBalanceByCurrency,
            availableBalanceByCurrency,
            balancesByAccount,
            concentration);
    }

    private static IReadOnlyDictionary<string, decimal> ComputeUpcomingObligationsByCurrency(
        DateTime asOfUtc,
        IReadOnlyList<Bill> bills,
        IReadOnlyList<Subscription> subscriptions,
        IReadOnlyList<PersonalRecurringBill> personalRecurringBills,
        IReadOnlyList<DebtRepayment> debtRepayments,
        DateTime lookaheadEndUtc)
    {
        var today = asOfUtc.Date;
        return bills
            .Where(x => x.NextDueDate.Date >= today && x.NextDueDate <= lookaheadEndUtc && x.ExpectedAmount.HasValue)
            .Select(x => (Currency: NormalizeCurrency(x.Currency), Amount: x.ExpectedAmount!.Value))
            .Concat(subscriptions
                .Where(x => x.RenewalDate.Date >= today && x.RenewalDate <= lookaheadEndUtc)
                .Select(x => (Currency: NormalizeCurrency(x.Currency), Amount: x.ExpectedAmount)))
            .Concat(personalRecurringBills
                .Where(x => x.NextDueDate.Date >= today && x.NextDueDate <= lookaheadEndUtc && x.ExpectedAmount.HasValue)
                .Select(x => (Currency: NormalizeCurrency(x.Currency), Amount: x.ExpectedAmount!.Value)))
            .Concat(debtRepayments
                .Where(x => x.NextDueDate.Date >= today && x.NextDueDate <= lookaheadEndUtc && x.ExpectedAmount.HasValue)
                .Select(x => (Currency: NormalizeCurrency(x.Currency), Amount: x.ExpectedAmount!.Value)))
            .GroupBy(x => x.Currency, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Amount), StringComparer.Ordinal);
    }

    private async Task<PersonalProfile?> LoadPersonalProfileAsync(
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

    private async Task<List<Order>> LoadOrdersAsync(
        Guid tenantId,
        Guid partyId,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        CancellationToken cancellationToken)
    {
        var orderIds = await _dbContext.OrderPartyRoles
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.PartyId == partyId)
            .Select(x => x.OrderId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (orderIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.Orders
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                && orderIds.Contains(x.Id)
                && x.CreatedAt >= windowStartUtc
                && x.CreatedAt <= windowEndUtc)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    private async Task<(Household? household, List<HouseholdMember> members)> LoadHouseholdAsync(
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

    private static CustomerInsightOrderHistory BuildOrderHistory(
        IReadOnlyList<Order> orders,
        DateTime windowStartUtc,
        DateTime windowEndUtc)
    {
        var completedCount = orders.Count(x => x.Status == OrderStatuses.Complete);
        var failedCount = orders.Count(x => x.Status is OrderStatuses.Failed or OrderStatuses.Cancelled or OrderStatuses.Expired);
        var pendingCount = orders.Count(x => x.Status is OrderStatuses.Pending or OrderStatuses.UnderReview or OrderStatuses.Approved or OrderStatuses.Transmitted or OrderStatuses.Draft);

        var recentOrders = orders
            .Take(50)
            .Select(x => new CustomerInsightRecentOrder(
                x.Id,
                string.IsNullOrWhiteSpace(x.OrderType) ? "Unknown" : x.OrderType.Trim(),
                string.IsNullOrWhiteSpace(x.Status) ? "Unknown" : x.Status.Trim(),
                NormalizeCurrency(x.CurrencyIn),
                decimal.Round(x.AmountIn, 2),
                x.CurrencyOut is null ? null : NormalizeCurrency(x.CurrencyOut),
                x.AmountOut.HasValue ? decimal.Round(x.AmountOut.Value, 2) : null,
                x.CreatedAt))
            .ToList();

        var byType = orders
            .GroupBy(x => string.IsNullOrWhiteSpace(x.OrderType) ? "Unknown" : x.OrderType.Trim())
            .OrderBy(x => x.Key)
            .Select(x => new CustomerInsightOrderTypeSummary(
                x.Key,
                x.Count(),
                x.Count(y => y.Status == OrderStatuses.Complete),
                x.Count(y => y.Status is OrderStatuses.Failed or OrderStatuses.Cancelled or OrderStatuses.Expired)))
            .ToList();

        return new CustomerInsightOrderHistory(
            windowStartUtc,
            windowEndUtc,
            orders.Count,
            completedCount,
            pendingCount,
            failedCount,
            recentOrders,
            byType);
    }

    private static CustomerInsightHouseholdContext BuildHouseholdContext(
        Household household,
        IReadOnlyList<HouseholdMember> members,
        Guid currentUserId)
    {
        var memberSummaries = members
            .OrderBy(x => x.UserId)
            .Select(x => new CustomerInsightHouseholdMemberSummary(
                x.UserId,
                string.IsNullOrWhiteSpace(x.Role) ? "member" : x.Role.Trim(),
                x.UserId == currentUserId))
            .ToList();

        return new CustomerInsightHouseholdContext(
            household.Id,
            string.IsNullOrWhiteSpace(household.Name) ? "Household" : household.Name.Trim(),
            members.Count,
            memberSummaries);
    }

    private static CustomerInsightIncomeSummary BuildIncomeSummary(
        IReadOnlyList<NormalizedTransaction> operationalTransactions,
        IReadOnlyList<NormalizedTransaction> previousOperationalTransactions,
        IReadOnlyList<NormalizedTransaction> behaviourTransactions,
        DateTime operationalWindowStartUtc,
        DateTime windowEndUtc)
    {
        var incomeTransactions = operationalTransactions.Where(x => x.IsIncome).ToList();
        var previousIncomeTransactions = previousOperationalTransactions.Where(x => x.IsIncome).ToList();
        var recurringIncomeEstimate = behaviourTransactions
            .Where(x => x.IsIncome)
            .GroupBy(x => new { x.Currency, x.SourceKey })
            .Select(x => new
            {
                x.Key.Currency,
                Amount = x.Sum(y => y.Amount),
                ObservedMonths = x.Select(y => GetMonthKey(y.OccurredAtUtc)).Distinct().Count()
            })
            .Where(x => x.ObservedMonths >= 2)
            .GroupBy(x => x.Currency)
            .OrderBy(x => x.Key)
            .Select(x => new CustomerInsightMoneyAmount(
                x.Key,
                decimal.Round(x.Sum(y => y.Amount / y.ObservedMonths), 2)))
            .ToList();

        return new CustomerInsightIncomeSummary(
            CustomerInsightSnapshotContract.OperationalWindowDays,
            operationalWindowStartUtc,
            windowEndUtc,
            GroupTransactionsByCurrency(incomeTransactions, false),
            recurringIncomeEstimate,
            DeriveIncomeCadence(behaviourTransactions.Where(x => x.IsIncome).Select(x => x.OccurredAtUtc).ToList()),
            incomeTransactions
                .GroupBy(x => new { x.Currency, x.SourceKey, x.SourceDisplay })
                .Select(x => new CustomerInsightSourceAmount(
                    x.Key.SourceDisplay,
                    x.Key.Currency,
                    decimal.Round(x.Sum(y => y.Amount), 2),
                    x.Count()))
                .OrderBy(x => x.Currency)
                .ThenByDescending(x => x.Amount)
                .ThenBy(x => x.Source)
                .Take(10)
                .ToList(),
            GroupTransactionsByAccount(incomeTransactions, false),
            BuildPeriodDeltas(incomeTransactions, previousIncomeTransactions, false));
    }

    private static CustomerInsightExpenseSummary BuildExpenseSummary(
        IReadOnlyList<NormalizedTransaction> operationalTransactions,
        IReadOnlyList<NormalizedTransaction> previousOperationalTransactions,
        IReadOnlyList<NormalizedTransaction> trendTransactions,
        IReadOnlyList<PersonalAccount> accounts,
        IReadOnlyList<CustomerInsightRecurringMerchantCandidate> recurringMerchantCandidates,
        DateTime operationalWindowStartUtc,
        DateTime windowEndUtc)
    {
        var expenseTransactions = operationalTransactions.Where(x => x.IsExpense).ToList();
        var previousExpenseTransactions = previousOperationalTransactions.Where(x => x.IsExpense).ToList();
        var recurringMerchantKeys = recurringMerchantCandidates
            .Select(x => NormalizeKey(x.Merchant))
            .ToHashSet(StringComparer.Ordinal);

        var fixedExpenses = expenseTransactions
            .Where(x => FixedExpenseCategories.Contains(x.Category)
                || (!string.IsNullOrWhiteSpace(x.MerchantKey) && recurringMerchantKeys.Contains(x.MerchantKey)))
            .ToList();

        var essentialExpenses = expenseTransactions
            .Where(x => EssentialExpenseCategories.Contains(x.Category))
            .ToList();

        var discretionaryExpenses = expenseTransactions
            .Where(x => !EssentialExpenseCategories.Contains(x.Category))
            .ToList();

        var totalOutflows = GroupTransactionsByCurrency(expenseTransactions, true);
        var fixedSpend = GroupTransactionsByCurrency(fixedExpenses, true);
        var essentialSpend = GroupTransactionsByCurrency(essentialExpenses, true);
        var discretionarySpend = GroupTransactionsByCurrency(discretionaryExpenses, true);
        var fixedLookup = fixedSpend.ToDictionary(x => x.Currency, x => x.Amount, StringComparer.Ordinal);

        var variableSpend = totalOutflows
            .Select(x => new CustomerInsightMoneyAmount(
                x.Currency,
                decimal.Round(x.Amount - (fixedLookup.TryGetValue(x.Currency, out var fixedAmount) ? fixedAmount : 0m), 2)))
            .ToList();

        var averageSpend = BuildAverageSpendByCurrency(trendTransactions.Where(x => x.IsExpense).ToList());
        _ = accounts;

        return new CustomerInsightExpenseSummary(
            CustomerInsightSnapshotContract.OperationalWindowDays,
            operationalWindowStartUtc,
            windowEndUtc,
            totalOutflows,
            fixedSpend,
            variableSpend,
            essentialSpend,
            discretionarySpend,
            GroupTransactionsByAccount(expenseTransactions, true),
            BuildPeriodDeltas(expenseTransactions, previousExpenseTransactions, true),
            averageSpend);
    }

    private static CustomerInsightCategoryInsights BuildCategoryInsights(
        IReadOnlyList<NormalizedTransaction> operationalTransactions,
        IReadOnlyList<NormalizedTransaction> previousOperationalTransactions,
        IReadOnlyList<NormalizedTransaction> behaviourTransactions,
        DateTime asOfUtc,
        DateTime operationalWindowStartUtc,
        DateTime windowEndUtc)
    {
        var expenseTransactions = operationalTransactions.Where(x => x.IsExpense).ToList();
        var previousExpenseTransactions = previousOperationalTransactions.Where(x => x.IsExpense).ToList();
        var currentTotalsByCurrency = expenseTransactions
            .GroupBy(x => x.Currency)
            .ToDictionary(x => x.Key, x => Math.Abs(x.Sum(y => y.Amount)), StringComparer.Ordinal);
        var previousByCategory = previousExpenseTransactions
            .GroupBy(x => new { x.Currency, x.Category })
            .ToDictionary(x => (x.Key.Currency, x.Key.Category), x => Math.Abs(x.Sum(y => y.Amount)));

        var categories = expenseTransactions
            .GroupBy(x => new { x.Currency, x.Category })
            .Select(x =>
            {
                var amount = Math.Abs(x.Sum(y => y.Amount));
                var totalForCurrency = currentTotalsByCurrency.TryGetValue(x.Key.Currency, out var total) ? total : 0m;
                var previousAmount = previousByCategory.TryGetValue((x.Key.Currency, x.Key.Category), out var prior) ? prior : 0m;
                decimal? deltaPercentage = previousAmount <= 0m
                    ? null
                    : decimal.Round((amount - previousAmount) / previousAmount * 100m, 2);

                return new CustomerInsightCategorySpend(
                    x.Key.Category,
                    x.Key.Currency,
                    decimal.Round(amount, 2),
                    totalForCurrency <= 0m ? 0m : decimal.Round(amount / totalForCurrency * 100m, 2),
                    x.Count(),
                    decimal.Round(previousAmount, 2),
                    deltaPercentage);
            })
            .ToList();

        return new CustomerInsightCategoryInsights(
            CustomerInsightSnapshotContract.OperationalWindowDays,
            operationalWindowStartUtc,
            windowEndUtc,
            TakeTopPerCurrency(categories, x => x.Currency, x => x.Amount),
            TakeTopPerCurrency(categories, x => x.Currency, x => x.ShareOfSpend),
            categories
                .OrderBy(x => x.Currency)
                .ThenByDescending(x => Math.Abs(x.DeltaPercentage ?? 0m))
                .ThenByDescending(x => x.Amount)
                .Take(10)
                .ToList(),
            BuildConcentrationRatios(
                categories,
                x => x.Currency,
                x => x.Amount,
                topN: 3),
            BuildCategoryMonthlyTrends(behaviourTransactions, asOfUtc));
    }

    private static CustomerInsightMerchantInsights BuildMerchantInsights(
        IReadOnlyList<NormalizedTransaction> operationalTransactions,
        IReadOnlyList<NormalizedTransaction> previousOperationalTransactions,
        IReadOnlyList<NormalizedTransaction> behaviourTransactions,
        DateTime asOfUtc,
        IReadOnlyList<CustomerInsightRecurringMerchantCandidate> recurringMerchantCandidates,
        DateTime operationalWindowStartUtc,
        DateTime windowEndUtc)
    {
        var expenseTransactions = operationalTransactions.Where(x => x.IsExpense).ToList();
        var previousExpenseTransactions = previousOperationalTransactions.Where(x => x.IsExpense).ToList();
        var currentTotalsByCurrency = expenseTransactions
            .GroupBy(x => x.Currency)
            .ToDictionary(x => x.Key, x => Math.Abs(x.Sum(y => y.Amount)), StringComparer.Ordinal);
        _ = previousExpenseTransactions;

        var merchants = expenseTransactions
            .GroupBy(x => new { x.Currency, x.MerchantKey, x.MerchantDisplay })
            .Select(x =>
            {
                var amount = Math.Abs(x.Sum(y => y.Amount));
                var totalForCurrency = currentTotalsByCurrency.TryGetValue(x.Key.Currency, out var total) ? total : 0m;

                return new CustomerInsightMerchantSpend(
                    x.Key.MerchantDisplay,
                    x.Key.Currency,
                    decimal.Round(amount, 2),
                    totalForCurrency <= 0m ? 0m : decimal.Round(amount / totalForCurrency * 100m, 2),
                    x.Count());
            })
            .ToList();

        var merchantFrequency = expenseTransactions
            .GroupBy(x => new { x.Currency, x.MerchantKey, x.MerchantDisplay })
            .Select(x => new CustomerInsightMerchantFrequency(
                x.Key.MerchantDisplay,
                x.Key.Currency,
                x.Count(),
                decimal.Round(Math.Abs(x.Sum(y => y.Amount)), 2)))
            .ToList();

        return new CustomerInsightMerchantInsights(
            CustomerInsightSnapshotContract.OperationalWindowDays,
            operationalWindowStartUtc,
            windowEndUtc,
            TakeTopPerCurrency(merchants, x => x.Currency, x => x.Amount),
            merchantFrequency
                .OrderBy(x => x.Currency)
                .ThenByDescending(x => x.TransactionCount)
                .ThenByDescending(x => x.Amount)
                .ThenBy(x => x.Merchant)
                .Take(10)
                .ToList(),
            recurringMerchantCandidates,
            BuildConcentrationRatios(
                merchants,
                x => x.Currency,
                x => x.Amount,
                topN: 3),
            BuildMerchantMonthlyTrends(behaviourTransactions, asOfUtc));
    }

    private static IReadOnlyList<CustomerInsightCategoryMonthlySeries> BuildCategoryMonthlyTrends(
        IReadOnlyList<NormalizedTransaction> behaviourTransactions,
        DateTime asOfUtc,
        int months = 6,
        int topCategories = 8)
    {
        var expenseTransactions = behaviourTransactions.Where(x => x.IsExpense).ToList();
        if (expenseTransactions.Count == 0)
        {
            return [];
        }

        var monthStarts = Enumerable.Range(0, months)
            .Select(offset => StartOfMonth(asOfUtc).AddMonths(-(months - offset - 1)))
            .ToList();

        var monthLabels = monthStarts
            .Select(m => m.ToString("yyyy-MM"))
            .ToList();

        var topCategoryKeys = expenseTransactions
            .GroupBy(x => new { x.Currency, x.Category })
            .Select(x => new { x.Key.Currency, x.Key.Category, Total = Math.Abs(x.Sum(y => y.Amount)) })
            .GroupBy(x => x.Currency)
            .SelectMany(g => g.OrderByDescending(x => x.Total).Take(topCategories))
            .Select(x => (x.Currency, x.Category))
            .ToHashSet();

        return expenseTransactions
            .Where(x => topCategoryKeys.Contains((x.Currency, x.Category)))
            .GroupBy(x => new { x.Currency, x.Category })
            .OrderBy(x => x.Key.Currency)
            .ThenBy(x => x.Key.Category)
            .Select(g =>
            {
                var amounts = monthStarts
                    .Select(monthStart =>
                    {
                        var monthEnd = monthStart.AddMonths(1).AddTicks(-1);
                        return decimal.Round(Math.Abs(g
                            .Where(x => x.OccurredAtUtc >= monthStart && x.OccurredAtUtc <= monthEnd)
                            .Sum(x => x.Amount)), 2);
                    })
                    .ToList();

                return new CustomerInsightCategoryMonthlySeries(
                    g.Key.Category,
                    g.Key.Currency,
                    new CustomerInsightMonthlySeries(monthLabels, amounts));
            })
            .ToList();
    }

    private static IReadOnlyList<CustomerInsightMerchantMonthlySeries> BuildMerchantMonthlyTrends(
        IReadOnlyList<NormalizedTransaction> behaviourTransactions,
        DateTime asOfUtc,
        int months = 6,
        int topMerchants = 5)
    {
        var expenseTransactions = behaviourTransactions
            .Where(x => x.IsExpense && !string.IsNullOrWhiteSpace(x.MerchantKey))
            .ToList();

        if (expenseTransactions.Count == 0)
        {
            return [];
        }

        var monthStarts = Enumerable.Range(0, months)
            .Select(offset => StartOfMonth(asOfUtc).AddMonths(-(months - offset - 1)))
            .ToList();

        var monthLabels = monthStarts
            .Select(m => m.ToString("yyyy-MM"))
            .ToList();

        var topMerchantKeys = expenseTransactions
            .GroupBy(x => new { x.Currency, x.MerchantKey, x.MerchantDisplay })
            .Select(x => new { x.Key.Currency, x.Key.MerchantKey, x.Key.MerchantDisplay, Total = Math.Abs(x.Sum(y => y.Amount)) })
            .GroupBy(x => x.Currency)
            .SelectMany(g => g.OrderByDescending(x => x.Total).Take(topMerchants))
            .Select(x => (x.Currency, x.MerchantKey, x.MerchantDisplay))
            .ToList();

        return topMerchantKeys
            .OrderBy(x => x.Currency)
            .ThenBy(x => x.MerchantDisplay)
            .Select(key =>
            {
                var merchantTransactions = expenseTransactions
                    .Where(x => x.Currency == key.Currency && x.MerchantKey == key.MerchantKey)
                    .ToList();

                var amounts = monthStarts
                    .Select(monthStart =>
                    {
                        var monthEnd = monthStart.AddMonths(1).AddTicks(-1);
                        return decimal.Round(Math.Abs(merchantTransactions
                            .Where(x => x.OccurredAtUtc >= monthStart && x.OccurredAtUtc <= monthEnd)
                            .Sum(x => x.Amount)), 2);
                    })
                    .ToList();

                return new CustomerInsightMerchantMonthlySeries(
                    key.MerchantDisplay,
                    key.Currency,
                    new CustomerInsightMonthlySeries(monthLabels, amounts));
            })
            .ToList();
    }

    private static CustomerInsightObligationInsights BuildObligationInsights(
        DateTime asOfUtc,
        IReadOnlyList<Bill> bills,
        IReadOnlyList<Subscription> subscriptions,
        IReadOnlyList<PersonalRecurringBill> personalRecurringBills,
        IReadOnlyList<DebtRepayment> debtRepayments,
        DateTime lookaheadEndUtc,
        CustomerInsightCashPosition cashPosition)
    {
        var today = asOfUtc.Date;

        var upcomingBills = bills
            .Where(x => x.NextDueDate.Date >= today && x.NextDueDate <= lookaheadEndUtc && x.ExpectedAmount.HasValue)
            .Select(x => new CustomerInsightCommitmentItem(
                "Bill",
                x.Id,
                string.IsNullOrWhiteSpace(x.Payee) ? "Unnamed bill" : x.Payee.Trim(),
                NormalizeCurrency(x.Currency),
                decimal.Round(x.ExpectedAmount ?? 0m, 2),
                x.NextDueDate,
                string.IsNullOrWhiteSpace(x.Frequency) ? null : x.Frequency.Trim()))
            .ToList();

        var upcomingSubscriptions = subscriptions
            .Where(x => x.RenewalDate.Date >= today && x.RenewalDate <= lookaheadEndUtc)
            .Select(x => new CustomerInsightCommitmentItem(
                "Subscription",
                x.Id,
                string.IsNullOrWhiteSpace(x.Merchant) ? "Unnamed subscription" : x.Merchant.Trim(),
                NormalizeCurrency(x.Currency),
                decimal.Round(x.ExpectedAmount, 2),
                x.RenewalDate,
                "monthly"))
            .ToList();

        var upcomingPersonalRecurringBills = personalRecurringBills
            .Where(x => x.NextDueDate.Date >= today && x.NextDueDate <= lookaheadEndUtc && x.ExpectedAmount.HasValue)
            .Select(x => new CustomerInsightCommitmentItem(
                "PersonalRecurringBill",
                x.Id,
                string.IsNullOrWhiteSpace(x.Payee) ? "Unnamed recurring bill" : x.Payee.Trim(),
                NormalizeCurrency(x.Currency),
                decimal.Round(x.ExpectedAmount ?? 0m, 2),
                x.NextDueDate,
                string.IsNullOrWhiteSpace(x.Frequency) ? null : x.Frequency.Trim()))
            .ToList();

        var upcomingDebtRepayments = debtRepayments
            .Where(x => x.NextDueDate.Date >= today && x.NextDueDate <= lookaheadEndUtc && x.ExpectedAmount.HasValue)
            .Select(x => new CustomerInsightCommitmentItem(
                "DebtRepayment",
                x.Id,
                string.IsNullOrWhiteSpace(x.CreditorName) ? "Unnamed debt" : x.CreditorName.Trim(),
                NormalizeCurrency(x.Currency),
                decimal.Round(x.ExpectedAmount ?? 0m, 2),
                x.NextDueDate,
                string.IsNullOrWhiteSpace(x.Frequency) ? null : x.Frequency.Trim()))
            .ToList();

        var supportObligations = upcomingBills
            .Where(x => bills.Any(y => y.Id == x.SourceId && y.LinkedOrderId.HasValue))
            .ToList();

        var totalUpcoming = upcomingBills
            .Concat(upcomingSubscriptions)
            .Concat(upcomingPersonalRecurringBills)
            .Concat(upcomingDebtRepayments)
            .GroupBy(x => x.Currency)
            .OrderBy(x => x.Key)
            .Select(x => new CustomerInsightMoneyAmount(x.Key, decimal.Round(x.Sum(y => y.Amount), 2)))
            .ToList();

        var balancesByCurrency = cashPosition.TotalBalanceByCurrency
            .ToDictionary(x => x.Currency, x => x.Amount, StringComparer.Ordinal);

        var coverageRatios = totalUpcoming
            .Select(x =>
            {
                var availableBalance = balancesByCurrency.TryGetValue(x.Currency, out var balance) ? balance : 0m;
                decimal? ratio = x.Amount <= 0m ? null : decimal.Round(availableBalance / x.Amount, 2);
                return new CustomerInsightCoverageRatio(x.Currency, availableBalance, x.Amount, ratio);
            })
            .ToList();

        return new CustomerInsightObligationInsights(
            CustomerInsightSnapshotContract.ObligationsLookaheadDays,
            today,
            lookaheadEndUtc,
            upcomingBills,
            upcomingSubscriptions,
            upcomingPersonalRecurringBills,
            upcomingDebtRepayments,
            supportObligations,
            totalUpcoming,
            coverageRatios);
    }

    private static CustomerInsightBudgetInsights BuildBudgetInsights(
        IReadOnlyList<Budget> budgets,
        IReadOnlyList<NormalizedTransaction> transactions,
        DateTime asOfUtc)
    {
        var usageRows = new List<CustomerInsightBudgetCategoryUsage>();
        var summaries = budgets
            .Select(x => new CustomerInsightBudgetSummary(
                x.Id,
                x.PeriodStart,
                string.IsNullOrWhiteSpace(x.PeriodType) ? "Unknown" : x.PeriodType.Trim(),
                x.Lines.Count,
                string.IsNullOrWhiteSpace(x.Status) ? "Unknown" : x.Status.Trim()))
            .ToList();

        foreach (var budget in budgets)
        {
            var periodEndUtc = ResolveBudgetPeriodEnd(budget.PeriodStart, budget.PeriodType);
            var effectiveEndUtc = asOfUtc < periodEndUtc ? asOfUtc : periodEndUtc;
            var elapsedDays = Math.Max((effectiveEndUtc.Date - budget.PeriodStart.Date).Days + 1, 1);
            var totalDays = Math.Max((periodEndUtc.Date - budget.PeriodStart.Date).Days + 1, 1);

            foreach (var line in budget.Lines.OrderBy(x => x.Id))
            {
                var lineCurrency = NormalizeCurrency(line.Currency);
                var template = BudgetCategoryTemplates.GetById(line.Category);
                var categoryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    NormalizeLower(line.Category, line.Category)
                };

                if (!string.IsNullOrWhiteSpace(template?.LinkedSpendingCategoryId))
                {
                    categoryIds.Add(template.LinkedSpendingCategoryId);
                }

                var spentAmount = transactions
                    .Where(x => x.IsExpense
                        && x.Currency == lineCurrency
                        && x.OccurredAtUtc >= budget.PeriodStart
                        && x.OccurredAtUtc <= periodEndUtc
                        && categoryIds.Contains(x.Category))
                    .Sum(x => Math.Abs(x.Amount));

                var projectedMonthEndAmount = decimal.Round(spentAmount / elapsedDays * totalDays, 2);
                var percentUsed = line.LimitAmount <= 0m ? 0m : decimal.Round(spentAmount / line.LimitAmount * 100m, 2);

                usageRows.Add(new CustomerInsightBudgetCategoryUsage(
                    budget.Id,
                    line.Id,
                    template?.Name ?? line.Category,
                    lineCurrency,
                    decimal.Round(line.LimitAmount, 2),
                    decimal.Round(spentAmount, 2),
                    percentUsed,
                    projectedMonthEndAmount,
                    line.LimitAmount > 0m && projectedMonthEndAmount > line.LimitAmount));
            }
        }

        return new CustomerInsightBudgetInsights(
            budgets.Count,
            summaries,
            usageRows
                .Where(x => x.PercentUsed >= CustomerInsightSnapshotContract.BudgetPressureThresholdPercent)
                .OrderByDescending(x => x.PercentUsed)
                .ThenBy(x => x.Category)
                .ToList(),
            usageRows
                .Where(x => x.PercentUsed > 100m)
                .OrderByDescending(x => x.PercentUsed)
                .ThenBy(x => x.Category)
                .ToList(),
            usageRows
                .Where(x => x.IsProjectedToOverspend)
                .OrderByDescending(x => x.ProjectedMonthEndAmount)
                .ThenBy(x => x.Category)
                .ToList());
    }

    private static CustomerInsightGoalInsights BuildGoalInsights(
        IReadOnlyList<Goal> goals,
        IReadOnlyList<NormalizedTransaction> transactions,
        DateTime trendWindowStartUtc,
        DateTime windowEndUtc)
    {
        var contributions = transactions
            .Where(x => x.OccurredAtUtc >= trendWindowStartUtc
                && x.OccurredAtUtc <= windowEndUtc
                && (SavingsContributionCategories.Contains(x.Category)
                    || (x.IsConfirmedTransfer && x.Category == TransactionCategoryReference.TransferOut)))
            .ToList();

        var averageMonthlyContributionByCurrency = contributions
            .GroupBy(x => x.Currency)
            .ToDictionary(
                x => x.Key,
                x => Math.Abs(x.Sum(y => y.Amount)) / Math.Max(CustomerInsightSnapshotContract.TrendWindowDays / 30m, 1m),
                StringComparer.Ordinal);

        var goalProgress = goals
            .Select(x =>
            {
                var remainingAmount = Math.Max(x.TargetAmount - x.ProgressAmount, 0m);
                var monthlyContribution = averageMonthlyContributionByCurrency.TryGetValue(NormalizeCurrency(x.Currency), out var value)
                    ? decimal.Round(value, 2)
                    : (decimal?)null;
                var monthsToTarget = monthlyContribution is > 0m
                    ? (int?)Math.Ceiling(remainingAmount / monthlyContribution.Value)
                    : null;

                return new CustomerInsightGoalProgress(
                    x.Id,
                    string.IsNullOrWhiteSpace(x.Name) ? "Unnamed goal" : x.Name.Trim(),
                    NormalizeCurrency(x.Currency),
                    decimal.Round(x.TargetAmount, 2),
                    decimal.Round(x.ProgressAmount, 2),
                    x.TargetAmount <= 0m ? 0m : decimal.Round(x.ProgressAmount / x.TargetAmount * 100m, 2),
                    x.TargetDate,
                    monthlyContribution,
                    monthsToTarget);
            })
            .OrderBy(x => x.TargetDate ?? DateTime.MaxValue)
            .ThenBy(x => x.Name)
            .ToList();

        return new CustomerInsightGoalInsights(
            goals.Count,
            goalProgress,
            DeriveSavingsContributionConsistency(contributions));
    }

    private static List<CustomerInsightSignal> BuildSignals(
        DateTime asOfUtc,
        IReadOnlyList<NormalizedTransaction> allTransactions,
        IReadOnlyList<NormalizedTransaction> operationalTransactions,
        IReadOnlyList<NormalizedTransaction> previousOperationalTransactions,
        CustomerInsightCashPosition cashPosition,
        CustomerInsightIncomeSummary incomeSummary,
        CustomerInsightExpenseSummary expenseSummary,
        CustomerInsightCategoryInsights categoryInsights,
        CustomerInsightMerchantInsights merchantInsights,
        CustomerInsightObligationInsights obligationInsights,
        CustomerInsightBudgetInsights budgetInsights,
        IReadOnlyList<Subscription> subscriptions,
        IReadOnlyList<CustomerInsightRecurringMerchantCandidate> recurringMerchantCandidates)
    {
        var signals = new List<CustomerInsightSignal>();

        AddRepaymentBurdenSignals(signals, allTransactions, asOfUtc);
        AddSavingsRateSignals(signals, allTransactions, asOfUtc);
        AddCategoryAccelerationSignals(signals, categoryInsights);
        AddRecurringCommitmentSignals(signals, expenseSummary);
        AddIncomeInstabilitySignals(signals, allTransactions, asOfUtc);
        AddCashBufferSignals(signals, asOfUtc, cashPosition, operationalTransactions);
        AddMerchantConcentrationSignals(signals, operationalTransactions, previousOperationalTransactions, merchantInsights);
        AddLateMonthSpendSignals(signals, allTransactions, asOfUtc);
        AddCashflowStressSignals(signals, obligationInsights);
        AddBudgetPressureSignals(signals, asOfUtc, budgetInsights);
        AddDormantSubscriptionSignals(signals, subscriptions, allTransactions, asOfUtc);
        AddRecurringMerchantSignals(signals, recurringMerchantCandidates, asOfUtc);

        return signals
            .OrderByDescending(x => SeverityRank(x.Severity))
            .ThenBy(x => x.Category)
            .ThenBy(x => x.SignalKey)
            .Take(20)
            .ToList();
    }

    private static void AddRepaymentBurdenSignals(List<CustomerInsightSignal> signals, IReadOnlyList<NormalizedTransaction> transactions, DateTime asOfUtc)
    {
        var seriesByCurrency = BuildMonthlyCurrencySeries(
            transactions.Where(x => x.IsExpense && x.Category == TransactionCategoryReference.LoanPayments),
            asOfUtc,
            3);

        foreach (var pair in seriesByCurrency)
        {
            if (pair.Value.Count < 2)
            {
                continue;
            }

            var first = pair.Value.First();
            var last = pair.Value.Last();

            if (first > 0m && last >= first * 1.15m && last - first >= 25m)
            {
                var increase = decimal.Round((last - first) / first * 100m, 2);
                signals.Add(new CustomerInsightSignal(
                    "repayment_burden_rising_over_time",
                    "debt_repayment",
                    "Repayment burden is rising",
                    $"Loan repayments increased by {increase}% over the recent observed months in {pair.Key}.",
                    increase >= 40m ? CustomerInsightSnapshotContract.SeverityHigh : CustomerInsightSnapshotContract.SeverityModerate,
                    GetConfidenceLevel(pair.Value.Count),
                    StartOfMonth(asOfUtc).AddMonths(-2),
                    asOfUtc,
                    ["metrics.expense.totalOutflowsByCurrency", "metrics.signals.debt_repayment"],
                    $"Monthly loan-payment outflows moved from {first} to {last} {pair.Key}."));
            }
        }
    }

    private static void AddSavingsRateSignals(List<CustomerInsightSignal> signals, IReadOnlyList<NormalizedTransaction> transactions, DateTime asOfUtc)
    {
        var incomeSeries = BuildMonthlyCurrencySeries(transactions.Where(x => x.IsIncome), asOfUtc, 3);
        var savingsSeries = BuildMonthlyCurrencySeries(
            transactions.Where(x => SavingsContributionCategories.Contains(x.Category)
                || (x.IsConfirmedTransfer && x.Category == TransactionCategoryReference.TransferOut)),
            asOfUtc,
            3,
            useAbsoluteAmount: true);

        foreach (var pair in incomeSeries)
        {
            if (!savingsSeries.TryGetValue(pair.Key, out var savingsValues) || pair.Value.Count < 2 || savingsValues.Count < 2)
            {
                continue;
            }

            var firstIncome = pair.Value.First();
            var lastIncome = pair.Value.Last();
            var firstSavings = savingsValues.First();
            var lastSavings = savingsValues.Last();

            if (firstIncome <= 0m || lastIncome <= 0m)
            {
                continue;
            }

            var firstRate = firstSavings / firstIncome;
            var lastRate = lastSavings / lastIncome;

            if (lastRate + 0.05m < firstRate)
            {
                signals.Add(new CustomerInsightSignal(
                    "savings_rate_falling_over_time",
                    "savings",
                    "Savings rate is falling",
                    $"Savings contributions as a share of income fell from {decimal.Round(firstRate * 100m, 2)}% to {decimal.Round(lastRate * 100m, 2)}% in {pair.Key}.",
                    firstRate - lastRate >= 0.10m ? CustomerInsightSnapshotContract.SeverityHigh : CustomerInsightSnapshotContract.SeverityModerate,
                    GetConfidenceLevel(pair.Value.Count),
                    StartOfMonth(asOfUtc).AddMonths(-2),
                    asOfUtc,
                    ["metrics.goals.savingsContributionConsistency", "metrics.income.totalInflowsByCurrency"],
                    $"Savings-to-income ratio decreased by {decimal.Round((firstRate - lastRate) * 100m, 2)} percentage points."));
            }
        }
    }

    private static void AddCategoryAccelerationSignals(List<CustomerInsightSignal> signals, CustomerInsightCategoryInsights categoryInsights)
    {
        foreach (var category in categoryInsights.CategoryTrendDeltas.Where(x => x.DeltaPercentage is >= 25m).Take(3))
        {
            signals.Add(new CustomerInsightSignal(
                $"category_spend_acceleration:{category.Currency}:{category.Category}",
                "spending",
                "Category spend is accelerating",
                $"Spending in {category.Category} rose by {category.DeltaPercentage}% in {category.Currency} compared with the previous operational window.",
                category.DeltaPercentage >= 50m ? CustomerInsightSnapshotContract.SeverityHigh : CustomerInsightSnapshotContract.SeverityModerate,
                GetConfidenceLevel(category.TransactionCount),
                categoryInsights.WindowStartUtc,
                categoryInsights.WindowEndUtc,
                ["metrics.categories.categoryTrendDeltas"],
                $"{category.Category} moved from {category.PreviousPeriodAmount} to {category.Amount} {category.Currency}."));
        }
    }

    private static void AddRecurringCommitmentSignals(List<CustomerInsightSignal> signals, CustomerInsightExpenseSummary expenseSummary)
    {
        foreach (var delta in expenseSummary.MonthOverMonthDeltaByCurrency.Where(x => x.DeltaPercentage is >= 15m))
        {
            signals.Add(new CustomerInsightSignal(
                $"recurring_commitment_growth:{delta.Currency}",
                "budget_pressure",
                "Recurring commitments are growing",
                $"Operational outflows increased by {delta.DeltaPercentage}% in {delta.Currency}, which can indicate growing fixed commitments.",
                delta.DeltaPercentage >= 30m ? CustomerInsightSnapshotContract.SeverityHigh : CustomerInsightSnapshotContract.SeverityModerate,
                CustomerInsightSnapshotContract.ConfidenceMedium,
                expenseSummary.WindowStartUtc.AddDays(-CustomerInsightSnapshotContract.OperationalWindowDays),
                expenseSummary.WindowEndUtc,
                ["metrics.expense.monthOverMonthDeltaByCurrency"],
                $"Operational outflows moved from {delta.PreviousAmount} to {delta.CurrentAmount} {delta.Currency}."));
        }
    }

    private static void AddIncomeInstabilitySignals(List<CustomerInsightSignal> signals, IReadOnlyList<NormalizedTransaction> transactions, DateTime asOfUtc)
    {
        var seriesByCurrency = BuildMonthlyCurrencySeries(transactions.Where(x => x.IsIncome), asOfUtc, 3);
        foreach (var pair in seriesByCurrency)
        {
            var series = pair.Value;
            if (series.Count < 3)
            {
                continue;
            }

            var average = series.Average();
            if (average <= 0m)
            {
                continue;
            }

            var standardDeviation = decimal.ToDouble((decimal)Math.Sqrt(series.Select(x => Math.Pow(decimal.ToDouble(x - average), 2)).Average()));
            var coefficientOfVariation = average == 0m ? 0d : standardDeviation / decimal.ToDouble(average);

            if (coefficientOfVariation > 0.25d)
            {
                signals.Add(new CustomerInsightSignal(
                    $"income_instability:{pair.Key}",
                    "cashflow",
                    "Income looks unstable",
                    $"Income variability is elevated in {pair.Key}, which can reduce cashflow predictability.",
                    coefficientOfVariation > 0.5d ? CustomerInsightSnapshotContract.SeverityHigh : CustomerInsightSnapshotContract.SeverityModerate,
                    CustomerInsightSnapshotContract.ConfidenceHigh,
                    StartOfMonth(asOfUtc).AddMonths(-2),
                    asOfUtc,
                    ["metrics.income.totalInflowsByCurrency"],
                    $"Coefficient of variation across the recent monthly income series was {Math.Round(coefficientOfVariation, 2)}."));
            }
        }
    }

    private static void AddCashBufferSignals(
        List<CustomerInsightSignal> signals,
        DateTime asOfUtc,
        CustomerInsightCashPosition cashPosition,
        IReadOnlyList<NormalizedTransaction> operationalTransactions)
    {
        var currentBalances = cashPosition.TotalBalanceByCurrency.ToDictionary(x => x.Currency, x => x.Amount, StringComparer.Ordinal);
        var netOperational = operationalTransactions
            .GroupBy(x => x.Currency)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Amount), StringComparer.Ordinal);

        foreach (var pair in currentBalances)
        {
            var currentBalance = pair.Value;
            var previousBalanceEstimate = currentBalance - (netOperational.TryGetValue(pair.Key, out var net) ? net : 0m);

            if (previousBalanceEstimate > 0m && currentBalance < previousBalanceEstimate * 0.8m)
            {
                signals.Add(new CustomerInsightSignal(
                    $"cash_buffer_deterioration:{pair.Key}",
                    "risk",
                    "Cash buffer is deteriorating",
                    $"Estimated available buffer fell meaningfully in {pair.Key} over the recent operational window.",
                    CustomerInsightSnapshotContract.SeverityHigh,
                    CustomerInsightSnapshotContract.ConfidenceMedium,
                    operationalTransactions.MinBy(x => x.OccurredAtUtc)?.OccurredAtUtc ?? asOfUtc,
                    operationalTransactions.MaxBy(x => x.OccurredAtUtc)?.OccurredAtUtc ?? asOfUtc,
                    ["metrics.cashPosition.totalBalanceByCurrency", "metrics.expense.totalOutflowsByCurrency"],
                    $"Estimated previous balance was {decimal.Round(previousBalanceEstimate, 2)} {pair.Key} and current balance is {currentBalance} {pair.Key}."));
            }
        }
    }

    private static void AddMerchantConcentrationSignals(
        List<CustomerInsightSignal> signals,
        IReadOnlyList<NormalizedTransaction> operationalTransactions,
        IReadOnlyList<NormalizedTransaction> previousOperationalTransactions,
        CustomerInsightMerchantInsights merchantInsights)
    {
        var currentShares = BuildTopMerchantShareByCurrency(operationalTransactions.Where(x => x.IsExpense).ToList());
        var previousShares = BuildTopMerchantShareByCurrency(previousOperationalTransactions.Where(x => x.IsExpense).ToList());

        foreach (var pair in currentShares)
        {
            var previousShare = previousShares.TryGetValue(pair.Key, out var value) ? value : 0m;
            if (pair.Value >= 35m && pair.Value - previousShare >= 15m)
            {
                signals.Add(new CustomerInsightSignal(
                    $"merchant_concentration_increase:{pair.Key}",
                    "risk",
                    "Merchant concentration is increasing",
                    $"A larger share of spending is concentrating with one merchant in {pair.Key}.",
                    CustomerInsightSnapshotContract.SeverityModerate,
                    CustomerInsightSnapshotContract.ConfidenceMedium,
                    merchantInsights.WindowStartUtc.AddDays(-CustomerInsightSnapshotContract.OperationalWindowDays),
                    merchantInsights.WindowEndUtc,
                    ["metrics.merchants.concentrationRatios"],
                    $"Top merchant share increased from {previousShare}% to {pair.Value}% in {pair.Key}."));
            }
        }
    }

    private static void AddLateMonthSpendSignals(List<CustomerInsightSignal> signals, IReadOnlyList<NormalizedTransaction> transactions, DateTime asOfUtc)
    {
        var spikes = transactions
            .Where(x => x.IsExpense)
            .GroupBy(x => GetMonthKey(x.OccurredAtUtc))
            .Select(x =>
            {
                var late = Math.Abs(x.Where(y => y.OccurredAtUtc.Day >= 25).Sum(y => y.Amount));
                var early = Math.Abs(x.Where(y => y.OccurredAtUtc.Day < 25).Sum(y => y.Amount));
                var lateDaily = late / 6m;
                var earlyDaily = early / 24m;
                return lateDaily > 0m && earlyDaily > 0m && lateDaily / earlyDaily > 1.2m;
            })
            .Count(x => x);

        if (spikes >= 2)
        {
            signals.Add(new CustomerInsightSignal(
                "sustained_late_month_spend_spikes",
                "trends",
                "Late-month spend spikes keep recurring",
                "Spending tends to accelerate in the last week of the month across multiple observed months.",
                CustomerInsightSnapshotContract.SeverityModerate,
                GetConfidenceLevel(spikes),
                StartOfMonth(asOfUtc).AddMonths(-3),
                asOfUtc,
                ["signals.sustained_late_month_spend_spikes"],
                $"Observed late-month spikes in {spikes} recent months."));
        }
    }

    private static void AddCashflowStressSignals(List<CustomerInsightSignal> signals, CustomerInsightObligationInsights obligationInsights)
    {
        foreach (var ratio in obligationInsights.CoverageRatios.Where(x => x.Ratio is < 1m))
        {
            signals.Add(new CustomerInsightSignal(
                $"cashflow_stress:{ratio.Currency}",
                "risk",
                "Upcoming obligations exceed available cash",
                $"Available cash covers less than one lookahead cycle of obligations in {ratio.Currency}.",
                CustomerInsightSnapshotContract.SeverityHigh,
                CustomerInsightSnapshotContract.ConfidenceHigh,
                obligationInsights.WindowStartUtc,
                obligationInsights.WindowEndUtc,
                ["metrics.obligations.coverageRatios"],
                $"Coverage ratio is {ratio.Ratio} with {ratio.AvailableBalance} available against {ratio.UpcomingObligations} upcoming obligations."));
        }
    }

    private static void AddBudgetPressureSignals(
        List<CustomerInsightSignal> signals,
        DateTime asOfUtc,
        CustomerInsightBudgetInsights budgetInsights)
    {
        foreach (var usage in budgetInsights.OverspentCategories.Take(3))
        {
            signals.Add(new CustomerInsightSignal(
                $"budget_pressure:{usage.Currency}:{usage.Category}",
                "budget_pressure",
                "Budget category is overspent",
                $"{usage.Category} has used {usage.PercentUsed}% of its budget in {usage.Currency}.",
                usage.PercentUsed >= 120m ? CustomerInsightSnapshotContract.SeverityHigh : CustomerInsightSnapshotContract.SeverityModerate,
                CustomerInsightSnapshotContract.ConfidenceHigh,
                asOfUtc.Date,
                asOfUtc,
                ["metrics.budgets.overspentCategories"],
                $"Spent {usage.SpentAmount} {usage.Currency} against a {usage.LimitAmount} {usage.Currency} limit."));
        }
    }

    private static void AddDormantSubscriptionSignals(
        List<CustomerInsightSignal> signals,
        IReadOnlyList<Subscription> subscriptions,
        IReadOnlyList<NormalizedTransaction> transactions,
        DateTime asOfUtc)
    {
        var cutoff = asOfUtc.AddDays(-60);
        var recentMerchantKeys = transactions
            .Where(x => x.IsExpense && x.OccurredAtUtc >= cutoff)
            .Select(x => x.MerchantKey)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var subscription in subscriptions.Where(x => !recentMerchantKeys.Contains(NormalizeKey(x.Merchant))).Take(3))
        {
            signals.Add(new CustomerInsightSignal(
                $"dormant_subscription:{NormalizeKey(subscription.Merchant)}",
                "subscriptions",
                "Subscription may be dormant",
                $"No matching recent transaction was found for {subscription.Merchant} in the last 60 days.",
                CustomerInsightSnapshotContract.SeverityLow,
                CustomerInsightSnapshotContract.ConfidenceMedium,
                cutoff,
                asOfUtc,
                ["metrics.obligations.subscriptions"],
                $"Expected renewal remains tracked for {subscription.Merchant} without a recent matching expense."));
        }
    }

    private static void AddRecurringMerchantSignals(
        List<CustomerInsightSignal> signals,
        IReadOnlyList<CustomerInsightRecurringMerchantCandidate> recurringMerchantCandidates,
        DateTime asOfUtc)
    {
        foreach (var merchant in recurringMerchantCandidates.Take(3))
        {
            signals.Add(new CustomerInsightSignal(
                $"recurring_spend_pattern:{merchant.Currency}:{NormalizeKey(merchant.Merchant)}",
                "subscriptions",
                "Recurring spend pattern detected",
                $"{merchant.Merchant} appears across multiple months and may represent a recurring commitment in {merchant.Currency}.",
                CustomerInsightSnapshotContract.SeverityLow,
                GetConfidenceLevel(merchant.ObservedMonths),
                StartOfMonth(asOfUtc).AddMonths(-merchant.ObservedMonths + 1),
                asOfUtc,
                ["metrics.merchants.recurringMerchantCandidates"],
                $"Observed {merchant.TransactionCount} transactions across {merchant.ObservedMonths} months."));
        }
    }

    private static CustomerInsightRiskOverview BuildRiskOverview(
        CustomerInsightObligationInsights obligationInsights,
        CustomerInsightBudgetInsights budgetInsights,
        CustomerInsightCategoryInsights categoryInsights,
        CustomerInsightMerchantInsights merchantInsights,
        IReadOnlyList<CustomerInsightSignal> signals)
    {
        var minimumCoverageRatio = obligationInsights.CoverageRatios
            .Where(x => x.Ratio.HasValue)
            .Select(x => x.Ratio!.Value)
            .DefaultIfEmpty(3m)
            .Min();

        var cashflowStress = minimumCoverageRatio < 1m
            ? CustomerInsightSnapshotContract.SeverityHigh
            : minimumCoverageRatio < 2m
                ? CustomerInsightSnapshotContract.SeverityModerate
                : CustomerInsightSnapshotContract.SeverityLow;

        var budgetPressure = budgetInsights.OverspentCategories.Count > 0
            ? CustomerInsightSnapshotContract.SeverityHigh
            : budgetInsights.CategoriesAboveThreshold.Count > 0
                ? CustomerInsightSnapshotContract.SeverityModerate
                : CustomerInsightSnapshotContract.SeverityLow;

        var concentrationRisks = categoryInsights.ConcentrationRatios
            .Where(x => x.Ratio >= 50m)
            .Select(x => $"Category concentration is high in {x.Currency} ({x.Ratio}%).")
            .Concat(merchantInsights.ConcentrationRatios
                .Where(x => x.Ratio >= 40m)
                .Select(x => $"Merchant concentration is high in {x.Currency} ({x.Ratio}%)."))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var unusualActivityIndicators = signals
            .Where(x => x.Category is "risk" or "trends" or "spending")
            .Where(x => x.Severity is CustomerInsightSnapshotContract.SeverityHigh or CustomerInsightSnapshotContract.SeverityCritical)
            .Select(x => x.Title)
            .Distinct(StringComparer.Ordinal)
            .Take(5)
            .ToList();

        return new CustomerInsightRiskOverview(
            cashflowStress,
            budgetPressure,
            concentrationRisks,
            cashflowStress,
            unusualActivityIndicators);
    }

    private static List<CustomerInsightRecurringMerchantCandidate> BuildRecurringMerchantCandidates(
        IReadOnlyList<NormalizedTransaction> transactions)
    {
        return transactions
            .Where(x => x.IsExpense && !string.IsNullOrWhiteSpace(x.MerchantKey))
            .GroupBy(x => new { x.Currency, x.MerchantKey, x.MerchantDisplay })
            .Select(x => new
            {
                x.Key.Currency,
                x.Key.MerchantDisplay,
                Amount = Math.Abs(x.Sum(y => y.Amount)),
                ObservedMonths = x.Select(y => GetMonthKey(y.OccurredAtUtc)).Distinct().Count(),
                TransactionCount = x.Count()
            })
            .Where(x => x.ObservedMonths >= 2)
            .OrderBy(x => x.Currency)
            .ThenByDescending(x => x.Amount)
            .ThenBy(x => x.MerchantDisplay)
            .Select(x => new CustomerInsightRecurringMerchantCandidate(
                x.MerchantDisplay,
                x.Currency,
                decimal.Round(x.Amount / x.ObservedMonths, 2),
                x.ObservedMonths,
                x.TransactionCount))
            .ToList();
    }

    private static string ComputeSourceHash(
        Guid tenantId,
        Guid userId,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        CustomerInsightCoverage coverage,
        IReadOnlyList<PersonalAccount> accounts,
        IReadOnlyList<NormalizedTransaction> transactions,
        IReadOnlyList<Bill> bills,
        IReadOnlyList<Subscription> subscriptions,
        IReadOnlyList<Budget> budgets,
        IReadOnlyList<Goal> goals)
    {
        var hashEnvelope = new
        {
            TenantId = tenantId,
            UserId = userId,
            WindowStartUtc = windowStartUtc,
            WindowEndUtc = windowEndUtc,
            GeneratorVersion = CustomerInsightSnapshotContract.GeneratorVersion,
            SchemaVersion = CustomerInsightSnapshotContract.SchemaVersion,
            DeterministicConfig = new
            {
                CustomerInsightSnapshotContract.OperationalWindowDays,
                CustomerInsightSnapshotContract.TrendWindowDays,
                CustomerInsightSnapshotContract.BehaviourWindowDays,
                CustomerInsightSnapshotContract.ObligationsLookaheadDays,
                CustomerInsightSnapshotContract.BudgetPressureThresholdPercent,
                MonetaryPolicy = CustomerInsightSnapshotContract.MonetaryPolicyNativeCurrency,
                TransferPolicy = CustomerInsightSnapshotContract.TransferPolicyNormalizedTransfers
            },
            Coverage = new
            {
                coverage.IsPartial,
                AvailableDomains = coverage.AvailableDomains.OrderBy(x => x).ToList(),
                MissingDomains = coverage.MissingDomains.OrderBy(x => x).ToList(),
                OmittedSections = coverage.OmittedSections.OrderBy(x => x).ToList()
            },
            Accounts = accounts
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    Currency = NormalizeCurrency(x.Currency),
                    Name = NormalizeKey(x.Name),
                    AccountType = NormalizeKey(x.AccountType),
                    Status = NormalizeKey(x.Status),
                    x.IsArchived,
                    x.CurrentBalance,
                    x.BalanceAsOf
                })
                .ToList(),
            Transactions = transactions
                .OrderBy(x => x.OccurredAtUtc)
                .ThenBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.PersonalAccountId,
                    x.OccurredAtUtc,
                    x.Amount,
                    x.Currency,
                    x.Category,
                    x.SubCategory,
                    x.NormalizedKind,
                    Merchant = x.MerchantKey,
                    Source = x.SourceKey
                })
                .ToList(),
            Bills = bills
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    Payee = NormalizeKey(x.Payee),
                    Currency = NormalizeCurrency(x.Currency),
                    x.ExpectedAmount,
                    x.NextDueDate,
                    Frequency = NormalizeKey(x.Frequency),
                    Status = NormalizeKey(x.Status),
                    x.LinkedOrderId
                })
                .ToList(),
            Subscriptions = subscriptions
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    Merchant = NormalizeKey(x.Merchant),
                    Currency = NormalizeCurrency(x.Currency),
                    x.ExpectedAmount,
                    x.RenewalDate,
                    Status = NormalizeKey(x.Status)
                })
                .ToList(),
            Budgets = budgets
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.PeriodStart,
                    PeriodType = NormalizeKey(x.PeriodType),
                    Status = NormalizeKey(x.Status),
                    Lines = x.Lines
                        .OrderBy(y => y.Id)
                        .Select(y => new
                        {
                            y.Id,
                            Category = NormalizeKey(y.Category),
                            Currency = NormalizeCurrency(y.Currency),
                            y.LimitAmount
                        })
                        .ToList()
                })
                .ToList(),
            Goals = goals
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    Name = NormalizeKey(x.Name),
                    Currency = NormalizeCurrency(x.Currency),
                    x.TargetAmount,
                    x.ProgressAmount,
                    x.TargetDate,
                    Status = NormalizeKey(x.Status),
                    x.FundingAccountId
                })
                .ToList()
        };

        var canonicalJson = JsonSerializer.Serialize(hashEnvelope, JsonOptions);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static IReadOnlyList<CustomerInsightMoneyAmount> GroupTransactionsByCurrency(
        IReadOnlyList<NormalizedTransaction> transactions,
        bool useAbsoluteAmount)
    {
        return transactions
            .GroupBy(x => x.Currency)
            .OrderBy(x => x.Key)
            .Select(x => new CustomerInsightMoneyAmount(
                x.Key,
                decimal.Round(useAbsoluteAmount ? Math.Abs(x.Sum(y => y.Amount)) : x.Sum(y => y.Amount), 2)))
            .ToList();
    }

    private static IReadOnlyList<CustomerInsightAccountFlow> GroupTransactionsByAccount(
        IReadOnlyList<NormalizedTransaction> transactions,
        bool useAbsoluteAmount)
    {
        return transactions
            .GroupBy(x => new { x.Currency, x.PersonalAccountId, x.AccountName })
            .OrderBy(x => x.Key.Currency)
            .ThenByDescending(x => useAbsoluteAmount ? Math.Abs(x.Sum(y => y.Amount)) : x.Sum(y => y.Amount))
            .ThenBy(x => x.Key.AccountName)
            .Select(x => new CustomerInsightAccountFlow(
                x.Key.PersonalAccountId,
                x.Key.AccountName,
                x.Key.Currency,
                decimal.Round(useAbsoluteAmount ? Math.Abs(x.Sum(y => y.Amount)) : x.Sum(y => y.Amount), 2),
                x.Count()))
            .ToList();
    }

    private static IReadOnlyList<CustomerInsightPeriodDelta> BuildPeriodDeltas(
        IReadOnlyList<NormalizedTransaction> currentTransactions,
        IReadOnlyList<NormalizedTransaction> previousTransactions,
        bool useAbsoluteAmount)
    {
        var previousLookup = previousTransactions
            .GroupBy(x => x.Currency)
            .ToDictionary(
                x => x.Key,
                x => useAbsoluteAmount ? Math.Abs(x.Sum(y => y.Amount)) : x.Sum(y => y.Amount),
                StringComparer.Ordinal);

        return currentTransactions
            .GroupBy(x => x.Currency)
            .OrderBy(x => x.Key)
            .Select(x =>
            {
                var currentAmount = useAbsoluteAmount ? Math.Abs(x.Sum(y => y.Amount)) : x.Sum(y => y.Amount);
                var previousAmount = previousLookup.TryGetValue(x.Key, out var value) ? value : 0m;
                var deltaAmount = currentAmount - previousAmount;
                decimal? deltaPercentage = previousAmount <= 0m ? null : decimal.Round(deltaAmount / previousAmount * 100m, 2);

                return new CustomerInsightPeriodDelta(
                    x.Key,
                    decimal.Round(currentAmount, 2),
                    decimal.Round(previousAmount, 2),
                    decimal.Round(deltaAmount, 2),
                    deltaPercentage);
            })
            .ToList();
    }

    private static IReadOnlyList<CustomerInsightAverageSpend> BuildAverageSpendByCurrency(IReadOnlyList<NormalizedTransaction> expenseTransactions)
    {
        if (expenseTransactions.Count == 0)
        {
            return [];
        }

        var startUtc = expenseTransactions.Min(x => x.OccurredAtUtc);
        var endUtc = expenseTransactions.Max(x => x.OccurredAtUtc);
        var observedDays = Math.Max((endUtc.Date - startUtc.Date).Days + 1, 1);

        return expenseTransactions
            .GroupBy(x => x.Currency)
            .OrderBy(x => x.Key)
            .Select(x =>
            {
                var amount = Math.Abs(x.Sum(y => y.Amount));
                var dailyAverage = amount / observedDays;
                return new CustomerInsightAverageSpend(
                    x.Key,
                    decimal.Round(dailyAverage * 7m, 2),
                    decimal.Round(dailyAverage * 30m, 2));
            })
            .ToList();
    }

    private static List<T> TakeTopPerCurrency<T>(
        IEnumerable<T> items,
        Func<T, string> currencySelector,
        Func<T, decimal> rankSelector,
        int limit = 5)
    {
        return items
            .GroupBy(currencySelector)
            .OrderBy(x => x.Key)
            .SelectMany(x => x.OrderByDescending(rankSelector).Take(limit))
            .ToList();
    }

    private static IReadOnlyList<CustomerInsightConcentrationRatio> BuildConcentrationRatios<T>(
        IEnumerable<T> items,
        Func<T, string> currencySelector,
        Func<T, decimal> amountSelector,
        int topN)
    {
        return items
            .GroupBy(currencySelector)
            .OrderBy(x => x.Key)
            .Select(x =>
            {
                var total = x.Sum(amountSelector);
                var topTotal = x.OrderByDescending(amountSelector).Take(topN).Sum(amountSelector);
                var ratio = total <= 0m ? 0m : topTotal / total * 100m;
                return new CustomerInsightConcentrationRatio(x.Key, decimal.Round(ratio, 2));
            })
            .ToList();
    }

    private static Dictionary<string, decimal> BuildTopMerchantShareByCurrency(IReadOnlyList<NormalizedTransaction> expenseTransactions)
    {
        return expenseTransactions
            .GroupBy(x => x.Currency)
            .ToDictionary(
                x => x.Key,
                x =>
                {
                    var total = Math.Abs(x.Sum(y => y.Amount));
                    if (total <= 0m)
                    {
                        return 0m;
                    }

                    var top = x.GroupBy(y => y.MerchantKey).Select(y => Math.Abs(y.Sum(z => z.Amount))).DefaultIfEmpty(0m).Max();
                    return decimal.Round(top / total * 100m, 2);
                },
                StringComparer.Ordinal);
    }

    private static Dictionary<string, List<decimal>> BuildMonthlyCurrencySeries(
        IEnumerable<NormalizedTransaction> transactions,
        DateTime asOfUtc,
        int months,
        bool useAbsoluteAmount = false)
    {
        var monthStarts = Enumerable.Range(0, months)
            .Select(offset => StartOfMonth(asOfUtc).AddMonths(-(months - offset - 1)))
            .ToList();

        return transactions
            .GroupBy(x => x.Currency)
            .ToDictionary(
                x => x.Key,
                x => monthStarts
                    .Select(monthStart =>
                    {
                        var monthEnd = monthStart.AddMonths(1).AddTicks(-1);
                        var amount = x
                            .Where(y => y.OccurredAtUtc >= monthStart && y.OccurredAtUtc <= monthEnd)
                            .Sum(y => y.Amount);
                        return decimal.Round(useAbsoluteAmount ? Math.Abs(amount) : amount, 2);
                    })
                    .ToList(),
                StringComparer.Ordinal);
    }

    private static string DeriveSavingsContributionConsistency(IReadOnlyList<NormalizedTransaction> contributions)
    {
        if (contributions.Count == 0)
        {
            return CustomerInsightSnapshotContract.ConfidenceLow;
        }

        var dominantCurrency = contributions
            .GroupBy(x => x.Currency)
            .OrderByDescending(x => Math.Abs(x.Sum(y => y.Amount)))
            .Select(x => x.Key)
            .First();

        var asOfUtc = contributions.Max(x => x.OccurredAtUtc);
        var monthlySeries = BuildMonthlyCurrencySeries(
            contributions.Where(x => x.Currency == dominantCurrency),
            asOfUtc,
            3,
            useAbsoluteAmount: true)[dominantCurrency];

        var average = monthlySeries.Average();
        if (average <= 0m)
        {
            return CustomerInsightSnapshotContract.ConfidenceLow;
        }

        var standardDeviation = Math.Sqrt(monthlySeries.Select(x => Math.Pow(decimal.ToDouble(x - average), 2)).Average());
        var coefficientOfVariation = standardDeviation / decimal.ToDouble(average);

        if (coefficientOfVariation <= 0.25d)
        {
            return CustomerInsightSnapshotContract.ConfidenceHigh;
        }

        if (coefficientOfVariation <= 0.5d)
        {
            return CustomerInsightSnapshotContract.ConfidenceMedium;
        }

        return CustomerInsightSnapshotContract.ConfidenceLow;
    }

    private static string DeriveIncomeCadence(IReadOnlyList<DateTime> incomeDates)
    {
        var ordered = incomeDates.OrderBy(x => x).ToList();
        if (ordered.Count < 2)
        {
            return "insufficient_history";
        }

        var intervals = ordered.Zip(ordered.Skip(1), (left, right) => (right.Date - left.Date).Days).ToList();
        var averageInterval = intervals.Average();

        if (averageInterval is >= 25 and <= 35)
        {
            return "monthly";
        }

        if (averageInterval is >= 12 and <= 18)
        {
            return "biweekly";
        }

        if (averageInterval is >= 5 and <= 9)
        {
            return "weekly";
        }

        return "irregular";
    }

    private static IReadOnlyList<string> CollectCurrencies(
        IReadOnlyList<PersonalAccount> accounts,
        IReadOnlyList<NormalizedTransaction> transactions,
        IReadOnlyList<Bill> bills,
        IReadOnlyList<Subscription> subscriptions,
        IReadOnlyList<Budget> budgets,
        IReadOnlyList<Goal> goals)
    {
        return accounts.Select(x => NormalizeCurrency(x.Currency))
            .Concat(transactions.Select(x => x.Currency))
            .Concat(bills.Select(x => NormalizeCurrency(x.Currency)))
            .Concat(subscriptions.Select(x => NormalizeCurrency(x.Currency)))
            .Concat(budgets.SelectMany(x => x.Lines.Select(y => NormalizeCurrency(y.Currency))))
            .Concat(goals.Select(x => NormalizeCurrency(x.Currency)))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x)
            .ToList();
    }

    private static NormalizedTransaction NormalizeTransaction(
        PersonalTransaction transaction,
        IReadOnlyDictionary<Guid, string> accountNameById)
    {
        var currency = NormalizeCurrency(transaction.Currency);
        var category = NormalizeLower(transaction.Category, TransactionCategoryReference.Uncategorized);
        var subCategory = NormalizeLower(transaction.SubCategory, null);
        var merchantDisplay = string.IsNullOrWhiteSpace(transaction.Merchant) ? "Unknown Merchant" : transaction.Merchant.Trim();
        var merchantKey = NormalizeKey(transaction.Merchant);
        var description = NormalizeDisplay(transaction.Description) ?? string.Empty;
        var sourceDisplay = !string.IsNullOrWhiteSpace(merchantKey)
            ? merchantDisplay
            : !string.IsNullOrWhiteSpace(description)
                ? description
                : category;
        var sourceKey = NormalizeKey(sourceDisplay);
        var normalizedKind = DeriveNormalizedKind(transaction, category, subCategory);

        var accountName = transaction.PersonalAccountId.HasValue
            && accountNameById.TryGetValue(transaction.PersonalAccountId.Value, out var resolvedName)
                ? resolvedName
                : "Unassigned";

        return new NormalizedTransaction(
            transaction.Id,
            transaction.PersonalAccountId,
            accountName,
            transaction.OccurredAt,
            transaction.Amount,
            currency,
            merchantDisplay,
            merchantKey,
            category,
            subCategory,
            normalizedKind,
            sourceDisplay,
            sourceKey,
            normalizedKind == TransactionCategoryReference.TypeTransfer,
            normalizedKind == TransactionCategoryReference.TypeIncome,
            normalizedKind == TransactionCategoryReference.TypeExpense);
    }

    private static string DeriveNormalizedKind(PersonalTransaction transaction, string category, string? subCategory)
    {
        var transactionType = NormalizeKey(transaction.TransactionType);
        if (transactionType == NormalizeKey(TransactionCategoryReference.TypeTransfer))
        {
            return TransactionCategoryReference.TypeTransfer;
        }

        if (TransferCategories.Contains(category) || string.Equals(subCategory, "own_account", StringComparison.OrdinalIgnoreCase))
        {
            return TransactionCategoryReference.TypeTransfer;
        }

        if (transactionType == NormalizeKey(TransactionCategoryReference.TypeIncome) || transaction.Amount > 0m)
        {
            return TransactionCategoryReference.TypeIncome;
        }

        if (transactionType == NormalizeKey(TransactionCategoryReference.TypeExpense) || transaction.Amount < 0m)
        {
            return TransactionCategoryReference.TypeExpense;
        }

        return "Other";
    }

    private static string GetConfidenceLevel(int observationCount)
    {
        if (observationCount >= 4)
        {
            return CustomerInsightSnapshotContract.ConfidenceHigh;
        }

        if (observationCount >= 2)
        {
            return CustomerInsightSnapshotContract.ConfidenceMedium;
        }

        return CustomerInsightSnapshotContract.ConfidenceLow;
    }

    private static int SeverityRank(string severity) => severity switch
    {
        CustomerInsightSnapshotContract.SeverityCritical => 4,
        CustomerInsightSnapshotContract.SeverityHigh => 3,
        CustomerInsightSnapshotContract.SeverityModerate => 2,
        _ => 1
    };

    private static DateTime ResolveWindowEnd(DateTime nowUtc) =>
        nowUtc.Date.AddDays(1).AddTicks(-1);

    private static DateTime ResolveOperationalWindowStart(DateTime nowUtc) =>
        nowUtc.Date.AddDays(-(CustomerInsightSnapshotContract.OperationalWindowDays - 1));

    private static DateTime ResolveTrendWindowStart(DateTime nowUtc) =>
        nowUtc.Date.AddDays(-(CustomerInsightSnapshotContract.TrendWindowDays - 1));

    private static DateTime ResolveBehaviourWindowStart(DateTime nowUtc) =>
        nowUtc.Date.AddDays(-(CustomerInsightSnapshotContract.BehaviourWindowDays - 1));

    private static DateTime ResolveLookaheadEnd(DateTime nowUtc) =>
        nowUtc.Date.AddDays(CustomerInsightSnapshotContract.ObligationsLookaheadDays).AddTicks(-1);

    private static DateTime ResolveBudgetPeriodEnd(DateTime periodStartUtc, string periodType)
    {
        if (string.Equals(periodType, "Weekly", StringComparison.OrdinalIgnoreCase))
        {
            return periodStartUtc.AddDays(7).AddTicks(-1);
        }

        return periodStartUtc.AddMonths(1).AddTicks(-1);
    }

    private static DateTime StartOfMonth(DateTime value) =>
        new(value.Year, value.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    private static string GetMonthKey(DateTime value) =>
        $"{value.Year:D4}-{value.Month:D2}";

    private static bool IsActiveStatus(string? status) =>
        string.Equals(status?.Trim(), "active", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status?.Trim(), "current", StringComparison.OrdinalIgnoreCase);

    private static bool IsArchivedStatus(string? status) =>
        string.Equals(status?.Trim(), "archived", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeCurrency(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "UNK" : value.Trim().ToUpperInvariant();

    private static string NormalizeKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    private static string NormalizeLower(string? value, string? fallback) =>
        string.IsNullOrWhiteSpace(value)
            ? (fallback ?? string.Empty)
            : value.Trim().ToLowerInvariant();

    private static string? NormalizeDisplay(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal sealed class CustomerInsightCoverageAccumulator
{
    private readonly HashSet<string> _availableDomains = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _missingDomains = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _omittedSections = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _warnings = [];

    public IReadOnlyList<string> Warnings => _warnings;

    public void MarkAvailable(string domainName)
    {
        _availableDomains.Add(domainName);
    }

    public void MarkMissing(string domainName, string omittedSection, string reason)
    {
        _missingDomains.Add(domainName);
        _omittedSections.Add(omittedSection);
        _warnings.Add($"{domainName} domain could not be loaded: {reason}");
    }

    public CustomerInsightCoverage Build()
    {
        return new CustomerInsightCoverage(
            _missingDomains.Count > 0,
            _availableDomains.OrderBy(x => x).ToList(),
            _missingDomains.OrderBy(x => x).ToList(),
            _omittedSections.OrderBy(x => x).ToList(),
            _warnings.Distinct(StringComparer.Ordinal).ToList());
    }
}

internal sealed record NormalizedTransaction(
    Guid Id,
    Guid? PersonalAccountId,
    string AccountName,
    DateTime OccurredAtUtc,
    decimal Amount,
    string Currency,
    string MerchantDisplay,
    string MerchantKey,
    string Category,
    string? SubCategory,
    string NormalizedKind,
    string SourceDisplay,
    string SourceKey,
    bool IsConfirmedTransfer,
    bool IsIncome,
    bool IsExpense);
