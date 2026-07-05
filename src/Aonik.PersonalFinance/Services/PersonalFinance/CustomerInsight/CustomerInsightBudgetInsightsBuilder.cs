using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Entities;

namespace Aonik.PersonalFinance.Services.CustomerInsight;

/// <summary>
/// Builds the <see cref="CustomerInsightBudgetInsights"/> section: per-budget
/// summaries, per-line usage rows (with month-end projection) and the filtered
/// subsets above the configured pressure threshold, overspent and projected
/// to overspend.
/// </summary>
internal static class CustomerInsightBudgetInsightsBuilder
{
    public static CustomerInsightBudgetInsights Build(
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
            var periodEndUtc = CustomerInsightWindows.ResolveBudgetPeriodEnd(budget.PeriodStart, budget.PeriodType);
            var effectiveEndUtc = asOfUtc < periodEndUtc ? asOfUtc : periodEndUtc;
            var elapsedDays = Math.Max((effectiveEndUtc.Date - budget.PeriodStart.Date).Days + 1, 1);
            var totalDays = Math.Max((periodEndUtc.Date - budget.PeriodStart.Date).Days + 1, 1);

            foreach (var line in budget.Lines.OrderBy(x => x.Id))
            {
                var lineCurrency = CustomerInsightNormalization.NormalizeCurrency(line.Currency);
                var template = BudgetCategoryTemplates.GetById(line.Category);
                var categoryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    CustomerInsightNormalization.NormalizeLower(line.Category, line.Category)
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
}
