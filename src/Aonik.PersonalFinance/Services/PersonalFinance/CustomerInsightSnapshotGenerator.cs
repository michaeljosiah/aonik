using System.Text.Json;
using System.Text.Json.Serialization;

using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Aonik.PersonalFinance.Services.CustomerInsight;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.PersonalFinance.Services;

/// <summary>
/// Orchestrates generation of a customer insight snapshot. Loads source data
/// via <see cref="CustomerInsightSourceDataLoader"/>, dispatches to the focused
/// section builders under <c>CustomerInsight/</c>, then assembles and serialises
/// the final <see cref="CustomerInsightSnapshotDocument"/> alongside its
/// deterministic source hash.
///
/// Consumes order history through <see cref="ICustomerOrderHistoryReader"/> so this
/// generator can move into PersonalFinance once the cluster is relocated (Spec 027).
/// </summary>
internal sealed class CustomerInsightSnapshotGenerator : ICustomerInsightSnapshotGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly PersonalFinanceDbContext _dbContext;
    private readonly ICustomerOrderHistoryReader _orderHistoryReader;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;

    public CustomerInsightSnapshotGenerator(
        PersonalFinanceDbContext dbContext,
        ICustomerOrderHistoryReader orderHistoryReader,
        ITenantProvider tenantProvider,
        IClock clock)
    {
        _dbContext = dbContext;
        _orderHistoryReader = orderHistoryReader;
        _tenantProvider = tenantProvider;
        _clock = clock;
    }

    public async Task<GeneratedCustomerInsightSnapshot> GenerateAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var asOfUtc = _clock.UtcNow;
        var windowEndUtc = CustomerInsightWindows.ResolveWindowEnd(asOfUtc);
        var operationalWindowStartUtc = CustomerInsightWindows.ResolveOperationalWindowStart(asOfUtc);
        var trendWindowStartUtc = CustomerInsightWindows.ResolveTrendWindowStart(asOfUtc);
        var behaviourWindowStartUtc = CustomerInsightWindows.ResolveBehaviourWindowStart(asOfUtc);
        var lookaheadEndUtc = CustomerInsightWindows.ResolveLookaheadEnd(asOfUtc);

        var coverageAccumulator = new CustomerInsightCoverageAccumulator();
        var loader = new CustomerInsightSourceDataLoader(_dbContext, _orderHistoryReader);

        var accounts = await loader.LoadAccountsAsync(tenantId, userId, coverageAccumulator, cancellationToken);
        var transactions = await loader.LoadTransactionsAsync(
            tenantId,
            userId,
            behaviourWindowStartUtc,
            windowEndUtc,
            coverageAccumulator,
            cancellationToken);

        var bills = await loader.LoadBillsAsync(tenantId, userId, coverageAccumulator, cancellationToken);
        var subscriptions = await loader.LoadSubscriptionsAsync(tenantId, userId, coverageAccumulator, cancellationToken);
        var personalRecurringBills = await loader.LoadPersonalRecurringBillsAsync(tenantId, userId, coverageAccumulator, cancellationToken);
        var debtRepayments = await loader.LoadDebtRepaymentsAsync(tenantId, userId, coverageAccumulator, cancellationToken);
        var budgets = await loader.LoadBudgetsAsync(tenantId, userId, coverageAccumulator, cancellationToken);
        var goals = await loader.LoadGoalsAsync(tenantId, userId, coverageAccumulator, cancellationToken);

        var accountNameById = accounts.ToDictionary(
            x => x.Id,
            x => string.IsNullOrWhiteSpace(x.Name) ? "Unnamed account" : x.Name.Trim());

        var normalizedTransactions = transactions
            .Select(x => CustomerInsightTransactionNormalizer.Normalize(x, accountNameById))
            .OrderBy(x => x.OccurredAtUtc)
            .ThenBy(x => x.Currency)
            .ThenBy(x => x.Amount)
            .ThenBy(x => x.Id)
            .ToList();

        var nonTransferTransactions = normalizedTransactions
            .Where(x => !x.IsConfirmedTransfer)
            .ToList();

        var operationalTransactions = CustomerInsightTransactionNormalizer.FilterByWindow(
            nonTransferTransactions, operationalWindowStartUtc, windowEndUtc);
        var previousOperationalTransactions = CustomerInsightTransactionNormalizer.FilterByWindow(
            nonTransferTransactions,
            operationalWindowStartUtc.AddDays(-CustomerInsightSnapshotContract.OperationalWindowDays),
            operationalWindowStartUtc.AddTicks(-1));
        var trendTransactions = CustomerInsightTransactionNormalizer.FilterByWindow(
            nonTransferTransactions, trendWindowStartUtc, windowEndUtc);

        var recurringMerchantCandidates = CustomerInsightExpenseSummaryBuilder.BuildRecurringMerchantCandidates(nonTransferTransactions);
        var upcomingObligationsByCurrency = CustomerInsightCashPositionBuilder.ComputeUpcomingObligationsByCurrency(
            asOfUtc, bills, subscriptions, personalRecurringBills, debtRepayments, lookaheadEndUtc);
        var cashPosition = CustomerInsightCashPositionBuilder.Build(accounts, upcomingObligationsByCurrency);
        var incomeSummary = CustomerInsightIncomeSummaryBuilder.Build(
            operationalTransactions, previousOperationalTransactions, nonTransferTransactions, operationalWindowStartUtc, windowEndUtc);
        var expenseSummary = CustomerInsightExpenseSummaryBuilder.Build(
            operationalTransactions,
            previousOperationalTransactions,
            trendTransactions,
            accounts,
            recurringMerchantCandidates,
            operationalWindowStartUtc,
            windowEndUtc);
        var categoryInsights = CustomerInsightCategoryInsightsBuilder.Build(
            operationalTransactions, previousOperationalTransactions, nonTransferTransactions, asOfUtc, operationalWindowStartUtc, windowEndUtc);
        var merchantInsights = CustomerInsightMerchantInsightsBuilder.Build(
            operationalTransactions, previousOperationalTransactions, nonTransferTransactions, asOfUtc, recurringMerchantCandidates, operationalWindowStartUtc, windowEndUtc);
        var obligationInsights = CustomerInsightObligationInsightsBuilder.Build(
            asOfUtc, bills, subscriptions, personalRecurringBills, debtRepayments, lookaheadEndUtc, cashPosition);
        var budgetInsights = CustomerInsightBudgetInsightsBuilder.Build(budgets, normalizedTransactions, asOfUtc);
        var goalInsights = CustomerInsightGoalInsightsBuilder.Build(goals, normalizedTransactions, trendWindowStartUtc, windowEndUtc);

        var metrics = new CustomerInsightMetrics(
            cashPosition,
            incomeSummary,
            expenseSummary,
            categoryInsights,
            merchantInsights,
            obligationInsights,
            budgetInsights,
            goalInsights);

        var signals = CustomerInsightSignalsBuilder.Build(
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

        var riskOverview = CustomerInsightRiskOverviewBuilder.Build(
            obligationInsights, budgetInsights, categoryInsights, merchantInsights, signals);

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

        var currencies = CustomerInsightSourceHasher.CollectCurrencies(
            accounts, normalizedTransactions, bills, subscriptions, budgets, goals);

        var profile = await loader.LoadPersonalProfileAsync(tenantId, userId, cancellationToken);

        IReadOnlyList<OrderHistoryItem> orderHistory = profile is not null
            ? await loader.LoadOptionalDomainAsync(
                ct => loader.LoadOrdersAsync(tenantId, profile.PartyId, behaviourWindowStartUtc, windowEndUtc, ct),
                "orders",
                "orderHistory",
                coverageAccumulator,
                cancellationToken)
            : [];

        var (household, householdMembers) = profile?.HouseholdId.HasValue == true
            ? await loader.LoadHouseholdAsync(tenantId, profile.HouseholdId.Value, coverageAccumulator, cancellationToken)
            : (null, new List<HouseholdMember>());

        var orderHistorySection = orderHistory.Count > 0
            ? CustomerInsightOrderHistoryBuilder.Build(orderHistory, behaviourWindowStartUtc, windowEndUtc)
            : null;

        var householdContextSection = household is not null
            ? CustomerInsightHouseholdContextBuilder.Build(household, householdMembers, userId)
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
        var sourceHash = CustomerInsightSourceHasher.ComputeSourceHash(
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
}
