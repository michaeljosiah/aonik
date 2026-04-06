using System.ComponentModel;
using Aonik.Finance.Agents.StructuredOutputs;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Finance.Agents.Tools;

internal sealed class SpendingIntelligenceTools
{
    private readonly IPersonalFinanceInsightsService _insightsService;
    private readonly IPersonalFinanceNarrativeInsightsService _narrativeInsightsService;
    private readonly ICustomerInsightSnapshotReader _snapshotReader;
    private readonly IBudgetService _budgetService;
    private readonly ICurrentUserProvider _currentUserProvider;

    private SpendingIntelligenceTools(
        IPersonalFinanceInsightsService insightsService,
        IPersonalFinanceNarrativeInsightsService narrativeInsightsService,
        ICustomerInsightSnapshotReader snapshotReader,
        IBudgetService budgetService,
        ICurrentUserProvider currentUserProvider)
    {
        _insightsService = insightsService;
        _narrativeInsightsService = narrativeInsightsService;
        _snapshotReader = snapshotReader;
        _budgetService = budgetService;
        _currentUserProvider = currentUserProvider;
    }

    [Description("Returns a compact factual dataset for spending analysis for a time window. Includes summary totals, top categories, top merchants, optional narrative insight, optional budget pressure signals, and optional snapshot-derived signals.")]
    public async Task<SpendingIntelligenceResult> AnalyseSpendingData(
        [Description("The user question or planning goal that this analysis supports")] string userQuestion,
        [Description("Start of the analysis period (UTC)")] DateTime periodStart,
        [Description("End of the analysis period (UTC)")] DateTime periodEnd,
        [Description("Optional account ID to scope the analysis to")] Guid? personalAccountId = null,
        [Description("Whether to include the AI spending narrative")] bool includeNarrative = true,
        [Description("Whether to include snapshot-derived signals if a current snapshot exists")] bool includeSnapshotSignals = true,
        [Description("Whether to include budget pressure signals from active budgets")] bool includeBudgetSignals = true,
        CancellationToken cancellationToken = default)
    {
        if (periodEnd < periodStart)
            throw new ArgumentException("periodEnd must be on or after periodStart.");

        var summary = await _insightsService.GetSpendingSummaryAsync(
            periodStart,
            periodEnd,
            personalAccountId,
            cancellationToken);

        var categories = await _insightsService.GetCategoryBreakdownAsync(
            periodStart,
            periodEnd,
            personalAccountId,
            cancellationToken);

        var merchants = await _insightsService.GetMerchantBreakdownAsync(
            periodStart,
            periodEnd,
            personalAccountId,
            top: 5,
            cancellationToken);

        PersonalSpendingNarrativeInsightResponse? narrative = null;
        if (includeNarrative)
        {
            narrative = await _narrativeInsightsService.GenerateSpendingNarrativeAsync(
                new GeneratePersonalSpendingNarrativeRequest(periodStart, periodEnd, personalAccountId),
                cancellationToken);
        }

        IReadOnlyList<SpendingIntelligenceBudgetSignal> budgetSignals = [];
        if (includeBudgetSignals)
        {
            budgetSignals = await BuildBudgetSignalsAsync(cancellationToken);
        }

        IReadOnlyList<SpendingIntelligenceSnapshotSignal> snapshotSignals = [];
        var warnings = new List<string>();
        if (includeSnapshotSignals)
        {
            (snapshotSignals, var snapshotWarnings) = await BuildSnapshotSignalsAsync(cancellationToken);
            warnings.AddRange(snapshotWarnings);
        }

        var entityRefs = BuildEntityReferences(personalAccountId, narrative, budgetSignals);
        var reasonCodes = BuildReasonCodes(categories, budgetSignals, snapshotSignals, narrative);
        var recommendedActions = BuildRecommendedActions(summary, categories, budgetSignals, snapshotSignals);
        var summaryText = BuildSummary(summary, categories, budgetSignals, snapshotSignals);
        var confidence = CalculateConfidence(summary, snapshotSignals, budgetSignals);

        return new SpendingIntelligenceResult(
            SpendingIntelligenceStructuredOutputContract.SchemaVersion,
            ResultType: "spending_intelligence",
            Summary: summaryText,
            Confidence: confidence,
            ReasonCodes: reasonCodes,
            EntityRefs: entityRefs,
            RecommendedActions: recommendedActions,
            Warnings: warnings,
            Payload: new SpendingIntelligencePayload(
                new SpendingIntelligenceAnalysisWindow(periodStart, periodEnd, personalAccountId),
                narrative is null
                    ? null
                    : new SpendingIntelligenceNarrative(
                        narrative.InsightId,
                        narrative.AiRunId,
                        narrative.Title,
                        narrative.Summary,
                        narrative.CreatedUtc),
                new SpendingIntelligenceSummary(
                    summary.Currency,
                    summary.TotalIncome,
                    summary.TotalExpense,
                    summary.NetAmount,
                    summary.TransactionCount),
                categories.Take(5)
                    .Select(x => new SpendingIntelligenceCategory(x.Category, x.TotalAmount, x.Percentage, x.TransactionCount))
                    .ToList(),
                merchants.Take(5)
                    .Select(x => new SpendingIntelligenceMerchant(x.Merchant, x.TotalAmount, x.TransactionCount))
                    .ToList(),
                budgetSignals,
                snapshotSignals));
    }

    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        var tools = new SpendingIntelligenceTools(
            serviceProvider.GetRequiredService<IPersonalFinanceInsightsService>(),
            serviceProvider.GetRequiredService<IPersonalFinanceNarrativeInsightsService>(),
            serviceProvider.GetRequiredService<ICustomerInsightSnapshotReader>(),
            serviceProvider.GetRequiredService<IBudgetService>(),
            serviceProvider.GetRequiredService<ICurrentUserProvider>());

        yield return AIFunctionFactory.Create(
            tools.AnalyseSpendingData,
            name: "pf_spending_analyse_data");
    }

    private async Task<(IReadOnlyList<SpendingIntelligenceSnapshotSignal> Signals, IReadOnlyList<string> Warnings)> BuildSnapshotSignalsAsync(
        CancellationToken cancellationToken)
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            return ([], ["Current user context was unavailable, so snapshot signals were omitted."]);
        }

        var snapshot = await _snapshotReader.GetCurrentSnapshotAsync(userId, cancellationToken);
        if (snapshot?.Snapshot is null)
        {
            return ([], ["No current customer insight snapshot was available for this user."]);
        }

        return (
            snapshot.Snapshot.Signals
                .Take(5)
                .Select(x => new SpendingIntelligenceSnapshotSignal(
                    x.SignalKey,
                    x.Category,
                    x.Title,
                    x.Description,
                    x.Severity,
                    x.Confidence))
                .ToList(),
            snapshot.Snapshot.Coverage.Warnings.ToList());
    }

    private async Task<IReadOnlyList<SpendingIntelligenceBudgetSignal>> BuildBudgetSignalsAsync(
        CancellationToken cancellationToken)
    {
        var budgets = await _budgetService.ListBudgetsAsync(cancellationToken);

        return budgets
            .SelectMany(x => x.LineItems.Select(line => new SpendingIntelligenceBudgetSignal(
                x.Name,
                line.Allocated,
                line.Spent,
                line.Allocated == 0 ? 0 : Math.Round((line.Spent / line.Allocated) * 100m, 2),
                line.Allocated > 0 && line.Spent > line.Allocated)))
            .Where(x => x.PercentUsed >= 70m || x.IsProjectedToOverspend)
            .OrderByDescending(x => x.PercentUsed)
            .Take(5)
            .ToList();
    }

    private static IReadOnlyList<SpendingIntelligenceEntityReference> BuildEntityReferences(
        Guid? personalAccountId,
        PersonalSpendingNarrativeInsightResponse? narrative,
        IReadOnlyList<SpendingIntelligenceBudgetSignal> budgetSignals)
    {
        var refs = new List<SpendingIntelligenceEntityReference>();

        if (personalAccountId.HasValue)
        {
            refs.Add(new SpendingIntelligenceEntityReference(
                "personal_account",
                personalAccountId.Value.ToString(),
                null));
        }

        if (narrative is not null)
        {
            refs.Add(new SpendingIntelligenceEntityReference(
                narrative.SubjectType,
                narrative.SubjectId.ToString(),
                narrative.Title));
        }

        refs.AddRange(budgetSignals.Select(signal => new SpendingIntelligenceEntityReference(
            "budget_category",
            signal.Category,
            signal.Category)));

        return refs;
    }

    private static IReadOnlyList<string> BuildReasonCodes(
        IReadOnlyList<CategorySpendingItemResponse> categories,
        IReadOnlyList<SpendingIntelligenceBudgetSignal> budgetSignals,
        IReadOnlyList<SpendingIntelligenceSnapshotSignal> snapshotSignals,
        PersonalSpendingNarrativeInsightResponse? narrative)
    {
        var codes = new List<string>();

        if (categories.Count > 0)
            codes.Add("category_breakdown_available");
        if (budgetSignals.Count > 0)
            codes.Add("budget_pressure_detected");
        if (snapshotSignals.Count > 0)
            codes.Add("snapshot_signals_available");
        if (narrative is not null)
            codes.Add("narrative_generated");

        return codes;
    }

    private static IReadOnlyList<SpendingIntelligenceRecommendedAction> BuildRecommendedActions(
        SpendingSummaryResponse summary,
        IReadOnlyList<CategorySpendingItemResponse> categories,
        IReadOnlyList<SpendingIntelligenceBudgetSignal> budgetSignals,
        IReadOnlyList<SpendingIntelligenceSnapshotSignal> snapshotSignals)
    {
        var actions = new List<SpendingIntelligenceRecommendedAction>();

        var topCategory = categories.OrderByDescending(x => x.TotalAmount).FirstOrDefault();
        if (topCategory is not null)
        {
            actions.Add(new SpendingIntelligenceRecommendedAction(
                "review_top_category",
                $"Review {topCategory.Category} spending",
                $"{topCategory.Category} is the largest spending category in this period at {topCategory.Percentage:N1}% of spend.",
                "high",
                topCategory.Category));
        }

        var overspend = budgetSignals.FirstOrDefault(x => x.IsProjectedToOverspend || x.PercentUsed >= 100m);
        if (overspend is not null)
        {
            actions.Add(new SpendingIntelligenceRecommendedAction(
                "budget_adjustment",
                $"Check {overspend.Category} budget pressure",
                $"{overspend.Category} is at {overspend.PercentUsed:N0}% of budget with spend of {overspend.SpentAmount:N2}.",
                "high",
                overspend.Category));
        }

        var elevatedSignal = snapshotSignals.FirstOrDefault(x => string.Equals(x.Severity, "high", StringComparison.OrdinalIgnoreCase));
        if (elevatedSignal is not null)
        {
            actions.Add(new SpendingIntelligenceRecommendedAction(
                "follow_snapshot_signal",
                elevatedSignal.Title,
                elevatedSignal.Description,
                "medium",
                elevatedSignal.SignalKey));
        }

        if (actions.Count == 0 && summary.TotalExpense > 0)
        {
            actions.Add(new SpendingIntelligenceRecommendedAction(
                "monitor_spending",
                "Keep tracking current spending",
                "No immediate pressure signal stands out, but maintaining visibility on category spending remains helpful.",
                "low",
                null));
        }

        return actions;
    }

    private static string BuildSummary(
        SpendingSummaryResponse summary,
        IReadOnlyList<CategorySpendingItemResponse> categories,
        IReadOnlyList<SpendingIntelligenceBudgetSignal> budgetSignals,
        IReadOnlyList<SpendingIntelligenceSnapshotSignal> snapshotSignals)
    {
        var topCategory = categories.OrderByDescending(x => x.TotalAmount).FirstOrDefault()?.Category;
        var budgetPressure = budgetSignals.FirstOrDefault(x => x.IsProjectedToOverspend || x.PercentUsed >= 90m)?.Category;
        var highSignal = snapshotSignals.FirstOrDefault(x => string.Equals(x.Severity, "high", StringComparison.OrdinalIgnoreCase))?.Title;

        var parts = new List<string>
        {
            $"Spending totalled {summary.TotalExpense:N2} {summary.Currency} across {summary.TransactionCount} transactions"
        };

        if (!string.IsNullOrWhiteSpace(topCategory))
            parts.Add($"with {topCategory} as the top category");
        if (!string.IsNullOrWhiteSpace(budgetPressure))
            parts.Add($"and budget pressure emerging in {budgetPressure}");
        if (!string.IsNullOrWhiteSpace(highSignal))
            parts.Add($"while snapshot monitoring flagged '{highSignal}'");

        return string.Join(" ", parts) + ".";
    }

    private static decimal CalculateConfidence(
        SpendingSummaryResponse summary,
        IReadOnlyList<SpendingIntelligenceSnapshotSignal> snapshotSignals,
        IReadOnlyList<SpendingIntelligenceBudgetSignal> budgetSignals)
    {
        var confidence = 0.55m;

        if (summary.TransactionCount >= 10)
            confidence += 0.15m;
        if (snapshotSignals.Count > 0)
            confidence += 0.15m;
        if (budgetSignals.Count > 0)
            confidence += 0.10m;

        return Math.Min(confidence, 0.95m);
    }
}
