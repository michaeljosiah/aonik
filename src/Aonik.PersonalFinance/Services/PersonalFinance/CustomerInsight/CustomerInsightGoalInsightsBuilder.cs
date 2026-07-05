using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Entities;

namespace Aonik.PersonalFinance.Services.CustomerInsight;

/// <summary>
/// Builds the <see cref="CustomerInsightGoalInsights"/> section: per-goal
/// progress (current vs target, monthly contribution, months-to-target) and
/// the savings-contribution consistency rating derived from monthly variance.
/// </summary>
internal static class CustomerInsightGoalInsightsBuilder
{
    public static CustomerInsightGoalInsights Build(
        IReadOnlyList<Goal> goals,
        IReadOnlyList<NormalizedTransaction> transactions,
        DateTime trendWindowStartUtc,
        DateTime windowEndUtc)
    {
        var contributions = transactions
            .Where(x => x.OccurredAtUtc >= trendWindowStartUtc
                && x.OccurredAtUtc <= windowEndUtc
                && (CustomerInsightNormalization.SavingsContributionCategories.Contains(x.Category)
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
                var monthlyContribution = averageMonthlyContributionByCurrency.TryGetValue(CustomerInsightNormalization.NormalizeCurrency(x.Currency), out var value)
                    ? decimal.Round(value, 2)
                    : (decimal?)null;
                var monthsToTarget = monthlyContribution is > 0m
                    ? (int?)Math.Ceiling(remainingAmount / monthlyContribution.Value)
                    : null;

                return new CustomerInsightGoalProgress(
                    x.Id,
                    string.IsNullOrWhiteSpace(x.Name) ? "Unnamed goal" : x.Name.Trim(),
                    CustomerInsightNormalization.NormalizeCurrency(x.Currency),
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

    public static string DeriveSavingsContributionConsistency(IReadOnlyList<NormalizedTransaction> contributions)
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
        var monthlySeries = CustomerInsightAggregations.BuildMonthlyCurrencySeries(
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
}
