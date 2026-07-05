using Aonik.PersonalFinance.Contracts.Models;

namespace Aonik.PersonalFinance.Services.CustomerInsight;

/// <summary>
/// Aggregates the previously-built sections into the high-level
/// <see cref="CustomerInsightRiskOverview"/>: cashflow stress level, budget
/// pressure level, concentration-risk descriptions and the unusual-activity
/// indicators distilled from the signals list.
/// </summary>
internal static class CustomerInsightRiskOverviewBuilder
{
    public static CustomerInsightRiskOverview Build(
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
}
