using System.ComponentModel;
using Aonik.Finance.Agents.StructuredOutputs;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Finance.Agents.Tools;

internal sealed class ObligationPlanningTools
{
    private readonly IBillService _billService;
    private readonly IFinancialLifeGraphService _financialLifeGraphService;
    private readonly IDashboardService _dashboardService;
    private readonly ICustomerInsightSnapshotReader _snapshotReader;
    private readonly ICurrentUserProvider _currentUserProvider;

    private ObligationPlanningTools(
        IBillService billService,
        IFinancialLifeGraphService financialLifeGraphService,
        IDashboardService dashboardService,
        ICustomerInsightSnapshotReader snapshotReader,
        ICurrentUserProvider currentUserProvider)
    {
        _billService = billService;
        _financialLifeGraphService = financialLifeGraphService;
        _dashboardService = dashboardService;
        _snapshotReader = snapshotReader;
        _currentUserProvider = currentUserProvider;
    }

    [Description("Returns a compact factual dataset for obligation planning. Includes due-soon bills and obligations, currency totals, dashboard spendable balance, optional snapshot coverage signals, and optional household context.")]
    public async Task<ObligationPlanningResult> AnalyseObligations(
        [Description("The user question or planning goal that this analysis supports")] string userQuestion,
        [Description("Number of days ahead to inspect for upcoming obligations")] int withinDays = 30,
        [Description("Whether to include snapshot-derived obligation coverage signals if a current snapshot exists")] bool includeSnapshotSignals = true,
        [Description("Whether to include household context if available")] bool includeHouseholdContext = true,
        CancellationToken cancellationToken = default)
    {
        if (withinDays <= 0)
            throw new ArgumentException("withinDays must be greater than 0.", nameof(withinDays));

        var upcomingBills = await _billService.GetUpcomingBillsAsync(withinDays, cancellationToken);
        var obligations = await _financialLifeGraphService.GetUpcomingObligationsAsync(withinDays, cancellationToken);
        var dashboard = await _dashboardService.GetDashboardAsync(cancellationToken);

        var obligationItems = obligations
            .Take(8)
            .Select(x => new ObligationPlanningObligation(
                x.ItemType,
                x.SourceId,
                x.DisplayName,
                x.Amount,
                x.Currency,
                x.DueDate,
                x.DaysUntilDue,
                x.Status))
            .ToList();

        var totals = obligations
            .Where(x => x.Amount.HasValue)
            .GroupBy(x => x.Currency, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ObligationPlanningCurrencyTotal(
                group.Key,
                group.Sum(x => x.Amount ?? 0m),
                group.Count()))
            .OrderByDescending(x => x.TotalAmount)
            .ToList();

        var coverageSignals = BuildCoverageSignals(totals, dashboard);
        var entityRefs = BuildEntityReferences(obligations, upcomingBills);
        var warnings = new List<string>();

        IReadOnlyList<ObligationPlanningSnapshotSignal> snapshotSignals = [];
        if (includeSnapshotSignals)
        {
            (snapshotSignals, var snapshotWarnings) = await BuildSnapshotSignalsAsync(cancellationToken);
            warnings.AddRange(snapshotWarnings);
        }

        ObligationPlanningHouseholdContext? householdContext = null;
        if (includeHouseholdContext)
        {
            householdContext = await BuildHouseholdContextAsync(cancellationToken);
        }

        var reasonCodes = BuildReasonCodes(obligations, coverageSignals, snapshotSignals, householdContext);
        var recommendedActions = BuildRecommendedActions(obligations, coverageSignals, snapshotSignals);
        var summary = BuildSummary(obligations, coverageSignals, snapshotSignals, withinDays);
        var confidence = CalculateConfidence(obligations, snapshotSignals, householdContext);

        return new ObligationPlanningResult(
            ObligationPlanningStructuredOutputContract.SchemaVersion,
            ResultType: "obligation_planning",
            Summary: summary,
            Confidence: confidence,
            ReasonCodes: reasonCodes,
            EntityRefs: entityRefs,
            RecommendedActions: recommendedActions,
            Warnings: warnings,
            Payload: new ObligationPlanningPayload(
                withinDays,
                obligationItems,
                totals,
                coverageSignals,
                snapshotSignals,
                householdContext));
    }

    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        var tools = new ObligationPlanningTools(
            serviceProvider.GetRequiredService<IBillService>(),
            serviceProvider.GetRequiredService<IFinancialLifeGraphService>(),
            serviceProvider.GetRequiredService<IDashboardService>(),
            serviceProvider.GetRequiredService<ICustomerInsightSnapshotReader>(),
            serviceProvider.GetRequiredService<ICurrentUserProvider>());

        yield return AIFunctionFactory.Create(
            tools.AnalyseObligations,
            name: "pf_obligations_analyse_data");
    }

    private async Task<(IReadOnlyList<ObligationPlanningSnapshotSignal> Signals, IReadOnlyList<string> Warnings)> BuildSnapshotSignalsAsync(
        CancellationToken cancellationToken)
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            return ([], ["Current user context was unavailable, so snapshot-backed obligation signals were omitted."]);
        }

        var snapshot = await _snapshotReader.GetCurrentSnapshotAsync(userId, cancellationToken);
        if (snapshot?.Snapshot is null)
        {
            return ([], ["No current customer insight snapshot was available for obligation analysis."]);
        }

        var obligationSignals = snapshot.Snapshot.Signals
            .Where(x => string.Equals(x.Category, "obligations", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Category, "budget", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Category, "cashflow", StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .Select(x => new ObligationPlanningSnapshotSignal(
                x.SignalKey,
                x.Category,
                x.Title,
                x.Description,
                x.Severity,
                x.Confidence))
            .ToList();

        return (obligationSignals, snapshot.Snapshot.Coverage.Warnings.ToList());
    }

    private async Task<ObligationPlanningHouseholdContext?> BuildHouseholdContextAsync(CancellationToken cancellationToken)
    {
        var context = await _financialLifeGraphService.GetHouseholdFinanceContextAsync(cancellationToken);

        return new ObligationPlanningHouseholdContext(
            context.HasHousehold,
            context.HouseholdId,
            context.MemberCount);
    }

    private static IReadOnlyList<ObligationPlanningCoverageSignal> BuildCoverageSignals(
        IReadOnlyList<ObligationPlanningCurrencyTotal> totals,
        DashboardResponse dashboard)
    {
        return totals.Select(total => new ObligationPlanningCoverageSignal(
                total.Currency,
                string.Equals(total.Currency, dashboard.Metrics.Currency, StringComparison.OrdinalIgnoreCase)
                    ? dashboard.Metrics.AvailableToSpend
                    : 0m,
                total.TotalAmount,
                total.TotalAmount == 0m
                    ? null
                    : Math.Round((string.Equals(total.Currency, dashboard.Metrics.Currency, StringComparison.OrdinalIgnoreCase)
                        ? dashboard.Metrics.AvailableToSpend
                        : 0m) / total.TotalAmount, 2)))
            .ToList();
    }

    private static IReadOnlyList<ObligationPlanningEntityReference> BuildEntityReferences(
        IReadOnlyList<UpcomingObligationResponse> obligations,
        IReadOnlyList<BillResponse> upcomingBills)
    {
        var refs = obligations
            .Take(8)
            .Select(x => new ObligationPlanningEntityReference(x.ItemType, x.SourceId.ToString(), x.DisplayName))
            .ToList();

        refs.AddRange(upcomingBills
            .Take(4)
            .Select(x => new ObligationPlanningEntityReference("bill", x.BillId.ToString(), x.Payee)));

        return refs;
    }

    private static IReadOnlyList<string> BuildReasonCodes(
        IReadOnlyList<UpcomingObligationResponse> obligations,
        IReadOnlyList<ObligationPlanningCoverageSignal> coverageSignals,
        IReadOnlyList<ObligationPlanningSnapshotSignal> snapshotSignals,
        ObligationPlanningHouseholdContext? householdContext)
    {
        var codes = new List<string>();

        if (obligations.Count > 0)
            codes.Add("upcoming_obligations_available");
        if (coverageSignals.Any(x => x.Ratio is < 1m))
            codes.Add("coverage_gap_detected");
        if (snapshotSignals.Count > 0)
            codes.Add("snapshot_signals_available");
        if (householdContext?.HasHousehold == true)
            codes.Add("household_context_available");

        return codes;
    }

    private static IReadOnlyList<ObligationPlanningRecommendedAction> BuildRecommendedActions(
        IReadOnlyList<UpcomingObligationResponse> obligations,
        IReadOnlyList<ObligationPlanningCoverageSignal> coverageSignals,
        IReadOnlyList<ObligationPlanningSnapshotSignal> snapshotSignals)
    {
        var actions = new List<ObligationPlanningRecommendedAction>();

        var urgentObligation = obligations.OrderBy(x => x.DaysUntilDue).FirstOrDefault();
        if (urgentObligation is not null)
        {
            actions.Add(new ObligationPlanningRecommendedAction(
                "review_due_soon_obligation",
                $"Review {urgentObligation.DisplayName}",
                $"{urgentObligation.DisplayName} is due in {urgentObligation.DaysUntilDue} day(s).",
                urgentObligation.DaysUntilDue <= 3 ? "high" : "medium",
                urgentObligation.SourceId.ToString()));
        }

        var coverageGap = coverageSignals.FirstOrDefault(x => x.Ratio is < 1m);
        if (coverageGap is not null)
        {
            actions.Add(new ObligationPlanningRecommendedAction(
                "address_coverage_gap",
                $"Close the {coverageGap.Currency} obligation gap",
                $"Available balance covers only {(coverageGap.Ratio ?? 0m):P0} of upcoming {coverageGap.Currency} obligations.",
                "high",
                coverageGap.Currency));
        }

        var highSignal = snapshotSignals.FirstOrDefault(x => string.Equals(x.Severity, "high", StringComparison.OrdinalIgnoreCase));
        if (highSignal is not null)
        {
            actions.Add(new ObligationPlanningRecommendedAction(
                "follow_snapshot_signal",
                highSignal.Title,
                highSignal.Description,
                "medium",
                highSignal.SignalKey));
        }

        return actions;
    }

    private static string BuildSummary(
        IReadOnlyList<UpcomingObligationResponse> obligations,
        IReadOnlyList<ObligationPlanningCoverageSignal> coverageSignals,
        IReadOnlyList<ObligationPlanningSnapshotSignal> snapshotSignals,
        int withinDays)
    {
        var dueSoon = obligations.Count(x => x.DaysUntilDue <= 7);
        var coverageGap = coverageSignals.FirstOrDefault(x => x.Ratio is < 1m)?.Currency;
        var highSignal = snapshotSignals.FirstOrDefault(x => string.Equals(x.Severity, "high", StringComparison.OrdinalIgnoreCase))?.Title;

        var parts = new List<string>
        {
            $"There are {obligations.Count} upcoming obligations within {withinDays} days"
        };

        if (dueSoon > 0)
            parts.Add($"with {dueSoon} due within the next week");
        if (!string.IsNullOrWhiteSpace(coverageGap))
            parts.Add($"and a likely coverage gap in {coverageGap}");
        if (!string.IsNullOrWhiteSpace(highSignal))
            parts.Add($"while snapshot monitoring flagged '{highSignal}'");

        return string.Join(" ", parts) + ".";
    }

    private static decimal CalculateConfidence(
        IReadOnlyList<UpcomingObligationResponse> obligations,
        IReadOnlyList<ObligationPlanningSnapshotSignal> snapshotSignals,
        ObligationPlanningHouseholdContext? householdContext)
    {
        var confidence = 0.60m;

        if (obligations.Count >= 3)
            confidence += 0.15m;
        if (snapshotSignals.Count > 0)
            confidence += 0.10m;
        if (householdContext is { HasHousehold: true })
            confidence += 0.05m;

        return Math.Min(confidence, 0.95m);
    }
}
