using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Services.PersonalFinance.CustomerInsight;

/// <summary>
/// Builds the <see cref="CustomerInsightCategoryInsights"/> section: top
/// categories by amount and share, biggest movers (period delta), category
/// concentration ratios and the per-month time series for the top categories
/// across the behaviour window.
/// </summary>
internal static class CustomerInsightCategoryInsightsBuilder
{
    public static CustomerInsightCategoryInsights Build(
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
            CustomerInsightAggregations.TakeTopPerCurrency(categories, x => x.Currency, x => x.Amount),
            CustomerInsightAggregations.TakeTopPerCurrency(categories, x => x.Currency, x => x.ShareOfSpend),
            categories
                .OrderBy(x => x.Currency)
                .ThenByDescending(x => Math.Abs(x.DeltaPercentage ?? 0m))
                .ThenByDescending(x => x.Amount)
                .Take(10)
                .ToList(),
            CustomerInsightAggregations.BuildConcentrationRatios(
                categories,
                x => x.Currency,
                x => x.Amount,
                topN: 3),
            BuildCategoryMonthlyTrends(behaviourTransactions, asOfUtc));
    }

    public static IReadOnlyList<CustomerInsightCategoryMonthlySeries> BuildCategoryMonthlyTrends(
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
            .Select(offset => CustomerInsightWindows.StartOfMonth(asOfUtc).AddMonths(-(months - offset - 1)))
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
}
