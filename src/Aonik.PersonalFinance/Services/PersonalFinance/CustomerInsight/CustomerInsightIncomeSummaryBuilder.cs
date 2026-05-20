using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Services.PersonalFinance.CustomerInsight;

/// <summary>
/// Builds the <see cref="CustomerInsightIncomeSummary"/> section: total inflows
/// per currency, recurring-income estimate, top sources, account-level inflows,
/// derived income cadence and the period-over-period delta.
/// </summary>
internal static class CustomerInsightIncomeSummaryBuilder
{
    public static CustomerInsightIncomeSummary Build(
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
                ObservedMonths = x.Select(y => CustomerInsightWindows.GetMonthKey(y.OccurredAtUtc)).Distinct().Count()
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
            CustomerInsightAggregations.GroupTransactionsByCurrency(incomeTransactions, false),
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
            CustomerInsightAggregations.GroupTransactionsByAccount(incomeTransactions, false),
            CustomerInsightAggregations.BuildPeriodDeltas(incomeTransactions, previousIncomeTransactions, false));
    }

    public static string DeriveIncomeCadence(IReadOnlyList<DateTime> incomeDates)
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
}
