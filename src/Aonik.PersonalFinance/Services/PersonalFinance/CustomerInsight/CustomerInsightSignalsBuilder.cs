using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;

namespace Aonik.Finance.Services.PersonalFinance.CustomerInsight;

/// <summary>
/// Builds the prioritised list of <see cref="CustomerInsightSignal"/>s by running
/// each individual signal detector (repayment burden, savings rate, category
/// acceleration, recurring commitment growth, income instability, cash buffer
/// deterioration, merchant concentration, late-month spend spikes, cashflow
/// stress, budget pressure, dormant subscriptions and recurring merchants),
/// then ranking them by severity and trimming to the top 20.
/// </summary>
internal static class CustomerInsightSignalsBuilder
{
    public static List<CustomerInsightSignal> Build(
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
        _ = incomeSummary;

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
            .OrderByDescending(x => CustomerInsightAggregations.SeverityRank(x.Severity))
            .ThenBy(x => x.Category)
            .ThenBy(x => x.SignalKey)
            .Take(20)
            .ToList();
    }

    private static void AddRepaymentBurdenSignals(List<CustomerInsightSignal> signals, IReadOnlyList<NormalizedTransaction> transactions, DateTime asOfUtc)
    {
        var seriesByCurrency = CustomerInsightAggregations.BuildMonthlyCurrencySeries(
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
                    CustomerInsightAggregations.GetConfidenceLevel(pair.Value.Count),
                    CustomerInsightWindows.StartOfMonth(asOfUtc).AddMonths(-2),
                    asOfUtc,
                    ["metrics.expense.totalOutflowsByCurrency", "metrics.signals.debt_repayment"],
                    $"Monthly loan-payment outflows moved from {first} to {last} {pair.Key}."));
            }
        }
    }

    private static void AddSavingsRateSignals(List<CustomerInsightSignal> signals, IReadOnlyList<NormalizedTransaction> transactions, DateTime asOfUtc)
    {
        var incomeSeries = CustomerInsightAggregations.BuildMonthlyCurrencySeries(transactions.Where(x => x.IsIncome), asOfUtc, 3);
        var savingsSeries = CustomerInsightAggregations.BuildMonthlyCurrencySeries(
            transactions.Where(x => CustomerInsightNormalization.SavingsContributionCategories.Contains(x.Category)
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
                    CustomerInsightAggregations.GetConfidenceLevel(pair.Value.Count),
                    CustomerInsightWindows.StartOfMonth(asOfUtc).AddMonths(-2),
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
                CustomerInsightAggregations.GetConfidenceLevel(category.TransactionCount),
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
        var seriesByCurrency = CustomerInsightAggregations.BuildMonthlyCurrencySeries(transactions.Where(x => x.IsIncome), asOfUtc, 3);
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
                    CustomerInsightWindows.StartOfMonth(asOfUtc).AddMonths(-2),
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
        var currentShares = CustomerInsightAggregations.BuildTopMerchantShareByCurrency(operationalTransactions.Where(x => x.IsExpense).ToList());
        var previousShares = CustomerInsightAggregations.BuildTopMerchantShareByCurrency(previousOperationalTransactions.Where(x => x.IsExpense).ToList());

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
            .GroupBy(x => CustomerInsightWindows.GetMonthKey(x.OccurredAtUtc))
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
                CustomerInsightAggregations.GetConfidenceLevel(spikes),
                CustomerInsightWindows.StartOfMonth(asOfUtc).AddMonths(-3),
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

        foreach (var subscription in subscriptions.Where(x => !recentMerchantKeys.Contains(CustomerInsightNormalization.NormalizeKey(x.Merchant))).Take(3))
        {
            signals.Add(new CustomerInsightSignal(
                $"dormant_subscription:{CustomerInsightNormalization.NormalizeKey(subscription.Merchant)}",
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
                $"recurring_spend_pattern:{merchant.Currency}:{CustomerInsightNormalization.NormalizeKey(merchant.Merchant)}",
                "subscriptions",
                "Recurring spend pattern detected",
                $"{merchant.Merchant} appears across multiple months and may represent a recurring commitment in {merchant.Currency}.",
                CustomerInsightSnapshotContract.SeverityLow,
                CustomerInsightAggregations.GetConfidenceLevel(merchant.ObservedMonths),
                CustomerInsightWindows.StartOfMonth(asOfUtc).AddMonths(-merchant.ObservedMonths + 1),
                asOfUtc,
                ["metrics.merchants.recurringMerchantCandidates"],
                $"Observed {merchant.TransactionCount} transactions across {merchant.ObservedMonths} months."));
        }
    }
}
