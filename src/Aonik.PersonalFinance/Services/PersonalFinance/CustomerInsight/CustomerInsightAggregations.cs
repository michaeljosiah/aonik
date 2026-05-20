using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Services.PersonalFinance.CustomerInsight;

/// <summary>
/// Pure aggregation primitives shared across insight builders: currency-keyed
/// totals, account-keyed flows, period deltas, average spend, top-per-currency
/// ranking, concentration ratios and monthly time-series construction. All
/// functions are deterministic and side-effect free.
/// </summary>
internal static class CustomerInsightAggregations
{
    public static IReadOnlyList<CustomerInsightMoneyAmount> GroupTransactionsByCurrency(
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

    public static IReadOnlyList<CustomerInsightAccountFlow> GroupTransactionsByAccount(
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

    public static IReadOnlyList<CustomerInsightPeriodDelta> BuildPeriodDeltas(
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

    public static IReadOnlyList<CustomerInsightAverageSpend> BuildAverageSpendByCurrency(IReadOnlyList<NormalizedTransaction> expenseTransactions)
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

    public static List<T> TakeTopPerCurrency<T>(
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

    public static IReadOnlyList<CustomerInsightConcentrationRatio> BuildConcentrationRatios<T>(
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

    public static Dictionary<string, decimal> BuildTopMerchantShareByCurrency(IReadOnlyList<NormalizedTransaction> expenseTransactions)
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

    public static Dictionary<string, List<decimal>> BuildMonthlyCurrencySeries(
        IEnumerable<NormalizedTransaction> transactions,
        DateTime asOfUtc,
        int months,
        bool useAbsoluteAmount = false)
    {
        var monthStarts = Enumerable.Range(0, months)
            .Select(offset => CustomerInsightWindows.StartOfMonth(asOfUtc).AddMonths(-(months - offset - 1)))
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

    public static string GetConfidenceLevel(int observationCount)
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

    public static int SeverityRank(string severity) => severity switch
    {
        CustomerInsightSnapshotContract.SeverityCritical => 4,
        CustomerInsightSnapshotContract.SeverityHigh => 3,
        CustomerInsightSnapshotContract.SeverityModerate => 2,
        _ => 1
    };
}
