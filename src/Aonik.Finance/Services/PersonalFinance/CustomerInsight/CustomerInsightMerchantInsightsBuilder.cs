using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Services.PersonalFinance.CustomerInsight;

/// <summary>
/// Builds the <see cref="CustomerInsightMerchantInsights"/> section: top
/// merchants by amount, merchant frequency, recurring-merchant candidates,
/// merchant concentration ratios and the per-month time series for the top
/// merchants across the behaviour window.
/// </summary>
internal static class CustomerInsightMerchantInsightsBuilder
{
    public static CustomerInsightMerchantInsights Build(
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
            CustomerInsightAggregations.TakeTopPerCurrency(merchants, x => x.Currency, x => x.Amount),
            merchantFrequency
                .OrderBy(x => x.Currency)
                .ThenByDescending(x => x.TransactionCount)
                .ThenByDescending(x => x.Amount)
                .ThenBy(x => x.Merchant)
                .Take(10)
                .ToList(),
            recurringMerchantCandidates,
            CustomerInsightAggregations.BuildConcentrationRatios(
                merchants,
                x => x.Currency,
                x => x.Amount,
                topN: 3),
            BuildMerchantMonthlyTrends(behaviourTransactions, asOfUtc));
    }

    public static IReadOnlyList<CustomerInsightMerchantMonthlySeries> BuildMerchantMonthlyTrends(
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
            .Select(offset => CustomerInsightWindows.StartOfMonth(asOfUtc).AddMonths(-(months - offset - 1)))
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
}
