using System.Text.Json;
using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.PersonalFinance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Services;

internal sealed class UserBriefProjector : IUserBriefProjector
{
    private readonly IUserBriefDataProvider _financeData;
    private readonly IUserBriefAiDataProvider _aiData;
    private readonly AgentsDbContext _agentsDbContext;
    private readonly ILogger<UserBriefProjector> _logger;

    public UserBriefProjector(
        IUserBriefDataProvider financeData,
        IUserBriefAiDataProvider aiData,
        AgentsDbContext agentsDbContext,
        ILogger<UserBriefProjector> logger)
    {
        _financeData = financeData;
        _aiData = aiData;
        _agentsDbContext = agentsDbContext;
        _logger = logger;
    }

    public async Task<UserBrief> ProjectAsync(
        Guid tenantId,
        Guid userId,
        UserBriefOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new UserBriefOptions();

        // ── Steps 1-3: Concurrent data retrieval ───────────────────────
        var financeRequest = new UserBriefFinancialDataRequest(
            options.BillLookaheadDays,
            options.SpendPeriodStart,
            options.SpendPeriodEnd);

        var financeTask = _financeData.GetFinancialDataAsync(tenantId, userId, financeRequest, cancellationToken);
        var insightsTask = _aiData.GetBehaviouralInsightsAsync(tenantId, userId, options.MaxBehaviouralInsights, cancellationToken);
        var memoryTask = _aiData.GetCurrentMemoryEntriesAsync(tenantId, userId, cancellationToken);

        await Task.WhenAll(financeTask, insightsTask, memoryTask);

        var financeData = await financeTask;
        var insights = await insightsTask;
        var memoryEntries = await memoryTask;
        var customerInsightSummary = financeData.CustomerInsightSnapshot is null
            ? null
            : await _aiData.GetCurrentCustomerInsightAiSummaryAsync(
                tenantId,
                userId,
                financeData.CustomerInsightSnapshot.SnapshotId,
                cancellationToken);

        // ── Step 4-5: Lightweight sequential lookups ───────────────────
        var conversationSummaries = await _agentsDbContext.ConversationSummaries
            .Where(s => s.TenantId == tenantId && s.UserId == userId)
            .OrderByDescending(s => s.SessionStartedAt)
            .Take(options.ConversationHistoryDepth)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // ── Assembly ───────────────────────────────────────────────────
        var userProfile = AssembleUserProfile(memoryEntries, financeData);
        var financialFocus = AssembleFinancialFocus(financeData);
        var currentState = AssembleCurrentState(financeData);
        var customerInsightSnapshot = AssembleCustomerInsightSnapshot(financeData.CustomerInsightSnapshot);
        var customerInsightAiInterpretation = AssembleCustomerInsightAiInterpretation(
            customerInsightSummary,
            financeData.CustomerInsightSnapshot);
        var cashflowRisk = DeriveCashflowRisk(financeData);
        var behaviouralInsights = AssembleBehaviouralInsights(insights);
        var conversationMemory = AssembleConversationMemory(conversationSummaries);
        var policyContext = DerivePolicyContext(memoryEntries);

        var brief = new UserBrief(
            userProfile,
            financialFocus,
            currentState,
            customerInsightSnapshot,
            customerInsightAiInterpretation,
            cashflowRisk,
            behaviouralInsights,
            conversationMemory,
            policyContext,
            DateTimeOffset.UtcNow);

        // ── Token budget enforcement ───────────────────────────────────
        return ApplyTokenBudget(brief, options.TokenBudget);
    }

    private static UserBriefProfile AssembleUserProfile(
        IReadOnlyList<UserBriefMemoryEntryData> memoryEntries,
        UserBriefFinancialData financeData)
    {
        string? GetMemoryValue(string key) =>
            memoryEntries.FirstOrDefault(e => e.Key == key)?.ValueJson;

        return new UserBriefProfile(
            PreferredName: TryUnquote(GetMemoryValue("identity.preferred_name")),
            CommunicationStyle: TryUnquote(GetMemoryValue("communication.style")),
            FinancialPosture: TryUnquote(GetMemoryValue("identity.financial_posture")),
            CorridorCountries: financeData.CorridorCountries,
            HouseholdContext: financeData.HouseholdContext
                ?? TryUnquote(GetMemoryValue("identity.household_context")),
            IncomeRhythm: TryUnquote(GetMemoryValue("fact.income_rhythm"))
                ?? GetMemoryValue("income.payday"),
            PrimaryNeeds: ParseJsonArray(GetMemoryValue("identity.primary_needs")));
    }

    private static UserBriefFinancialFocus AssembleFinancialFocus(UserBriefFinancialData data)
    {
        var goals = data.ActiveGoals.Select(g => new UserBriefGoal(
            g.GoalId, g.Name, g.TargetAmount, g.ProgressAmount,
            g.Currency, g.TargetDate, g.Status)).ToList();

        var obligations = data.SupportObligations.Select(o => new UserBriefObligation(
            o.DisplayName, o.Amount, o.Currency, o.Frequency, o.NextDueDate)).ToList();

        return new UserBriefFinancialFocus(goals, obligations);
    }

    private static UserBriefCurrentState AssembleCurrentState(UserBriefFinancialData data)
    {
        var cashSummary = new UserBriefCashSummary(
            data.TotalBalance, data.AvailableBalance, data.PrimaryCurrency);

        var bills = data.UpcomingBills.Select(b => new UserBriefBill(
            b.BillId, b.Payee, b.Amount, b.Currency, b.DueDate, b.Autopay)).ToList();

        var subscriptions = data.ActiveSubscriptions.Select(s => new UserBriefSubscription(
            s.SubscriptionId, s.Merchant, s.ExpectedAmount, s.Currency, s.RenewalDate)).ToList();

        var spendSummary = data.SpendSummary is not null
            ? new UserBriefSpendSummary(
                data.SpendSummary.TotalSpend,
                data.SpendSummary.TopCategories.Select(c => new UserBriefCategorySpend(
                    c.Category, c.Amount, c.Percentage)).ToList(),
                data.SpendSummary.PeriodStart,
                data.SpendSummary.PeriodEnd)
            : null;

        var budgetPressure = data.BudgetPressure.Select(b => new UserBriefBudgetPressure(
            b.Category, b.Budgeted, b.Actual, b.PercentUsed)).ToList();

        return new UserBriefCurrentState(cashSummary, bills, subscriptions, spendSummary, budgetPressure);
    }

    private static UserBriefCustomerInsightSnapshotSummary? AssembleCustomerInsightSnapshot(
        UserBriefCustomerInsightSnapshotData? snapshot)
    {
        if (snapshot is null)
        {
            return null;
        }

        var coverageWarnings = snapshot.IsPartial
            ? new[] { "Deterministic customer insight snapshot is partial." }
                .Concat(snapshot.CoverageWarnings)
                .Distinct(StringComparer.Ordinal)
                .ToList()
            : snapshot.CoverageWarnings;

        return new UserBriefCustomerInsightSnapshotSummary(
            snapshot.AsOfUtc,
            snapshot.WindowStartUtc,
            snapshot.WindowEndUtc,
            snapshot.IsPartial,
            coverageWarnings,
            snapshot.TotalBalanceByCurrency.Select(x => new UserBriefSnapshotMoney(x.Currency, x.Amount)).ToList(),
            snapshot.TotalInflowsByCurrency.Select(x => new UserBriefSnapshotMoney(x.Currency, x.Amount)).ToList(),
            snapshot.TotalOutflowsByCurrency.Select(x => new UserBriefSnapshotMoney(x.Currency, x.Amount)).ToList(),
            snapshot.TopCategorySpend.Select(x => new UserBriefSnapshotSpend(x.Name, x.Currency, x.Amount, x.ShareOfSpend)).ToList(),
            snapshot.TopMerchantSpend.Select(x => new UserBriefSnapshotSpend(x.Name, x.Currency, x.Amount, x.ShareOfSpend)).ToList(),
            snapshot.UpcomingObligationsByCurrency.Select(x => new UserBriefSnapshotMoney(x.Currency, x.Amount)).ToList(),
            snapshot.ObligationCoverageSummaries,
            snapshot.BudgetPressureCategories,
            snapshot.GoalProgressHighlights,
            snapshot.KeyBehaviourSignals.Select(x => new UserBriefSnapshotSignal(
                x.SignalKey,
                x.Category,
                x.Title,
                x.Description,
                x.Severity,
                x.Confidence)).ToList(),
            snapshot.RiskFlags);
    }

    private static UserBriefCustomerInsightAiInterpretation? AssembleCustomerInsightAiInterpretation(
        UserBriefCustomerInsightAiSummaryData? summary,
        UserBriefCustomerInsightSnapshotData? snapshot)
    {
        if (summary is null)
        {
            return null;
        }

        var caveats = snapshot?.IsPartial == true
            ? summary.Caveats
                .Concat(["Underlying deterministic snapshot is partial; treat AI interpretation as lower certainty."])
                .Distinct(StringComparer.Ordinal)
                .ToList()
            : summary.Caveats;

        return new UserBriefCustomerInsightAiInterpretation(
            summary.Headline,
            summary.Summary,
            summary.KeyObservations,
            summary.RecommendedFocusAreas,
            summary.ReferencedMetricKeys,
            caveats);
    }

    /// <summary>
    /// Low: available > 2x upcoming obligations.
    /// Moderate: available > 1x upcoming obligations.
    /// High: available &lt; upcoming obligations.
    /// </summary>
    private static CashflowRisk DeriveCashflowRisk(UserBriefFinancialData data)
    {
        var upcomingTotal = data.UpcomingBills.Sum(b => b.Amount ?? 0m);
        var available = data.AvailableBalance;

        if (upcomingTotal == 0) return CashflowRisk.Low;
        if (available >= upcomingTotal * 2) return CashflowRisk.Low;
        if (available >= upcomingTotal) return CashflowRisk.Moderate;
        return CashflowRisk.High;
    }

    private static IReadOnlyList<UserBriefBehaviouralInsight> AssembleBehaviouralInsights(
        IReadOnlyList<UserBriefInsightData> insights)
    {
        return insights.Select(i => new UserBriefBehaviouralInsight(
            i.InsightType, i.Title, i.Summary, i.Confidence)).ToList();
    }

    private static IReadOnlyList<UserBriefConversationMemory> AssembleConversationMemory(
        List<ConversationSummary> summaries)
    {
        return summaries.Select(s =>
        {
            var openLoops = ParseJsonList<UserBriefOpenLoop>(s.OpenLoopsJson);
            var outcomes = ParseJsonList<UserBriefRecommendationOutcome>(s.RecommendationOutcomesJson);

            return new UserBriefConversationMemory(
                s.SessionStartedAt,
                s.SummaryText,
                openLoops,
                outcomes);
        }).ToList();
    }

    private static UserBriefPolicyContext DerivePolicyContext(
        IReadOnlyList<UserBriefMemoryEntryData> memoryEntries)
    {
        // Default conservative policy — real implementation would read from agent configuration
        var riskTier = TryUnquote(
            memoryEntries.FirstOrDefault(e => e.Key == "policy.risk_tier")?.ValueJson) ?? "standard";

        return new UserBriefPolicyContext(
            RiskTier: riskTier,
            AiCanDo: ["view_balances", "categorise_transactions", "generate_insights", "send_reminders"],
            AiCannotDoWithoutApproval: ["initiate_payment", "create_order", "modify_bill", "cancel_subscription"]);
    }

    /// <summary>
    /// Estimates token count (~4 chars per token) and truncates lower-priority sections
    /// in priority order until the budget is met.
    /// </summary>
    private static UserBrief ApplyTokenBudget(UserBrief brief, int tokenBudget)
    {
        var json = JsonSerializer.Serialize(brief);
        var estimatedTokens = json.Length / 4;

        if (estimatedTokens <= tokenBudget) return brief;

        // Truncation pass 1: reduce behavioural insights to 3, then 1
        if (brief.BehaviouralInsights.Count > 3)
        {
            brief = brief with { BehaviouralInsights = brief.BehaviouralInsights.Take(3).ToList() };
            if (EstimateTokens(brief) <= tokenBudget) return brief;
        }
        if (brief.BehaviouralInsights.Count > 1)
        {
            brief = brief with { BehaviouralInsights = brief.BehaviouralInsights.Take(1).ToList() };
            if (EstimateTokens(brief) <= tokenBudget) return brief;
        }

        // Truncation pass 2: reduce conversation history to 1, then 0
        if (brief.RecentConversationMemory.Count > 1)
        {
            brief = brief with { RecentConversationMemory = brief.RecentConversationMemory.Take(1).ToList() };
            if (EstimateTokens(brief) <= tokenBudget) return brief;
        }

        if (brief.RecentConversationMemory.Count > 0)
        {
            brief = brief with { RecentConversationMemory = [] };
            if (EstimateTokens(brief) <= tokenBudget) return brief;
        }

        // Truncation pass 3: truncate live operational detail before canonical insight sections.
        if (brief.CurrentState.Subscriptions.Count > 5)
        {
            brief = brief with
            {
                CurrentState = brief.CurrentState with
                {
                    Subscriptions = brief.CurrentState.Subscriptions.Take(5).ToList()
                }
            };
            if (EstimateTokens(brief) <= tokenBudget) return brief;
        }

        // Truncation pass 4: truncate spend categories to top 3
        if (brief.CurrentState.SpendSummary is not null &&
            brief.CurrentState.SpendSummary.TopCategories.Count > 3)
        {
            brief = brief with
            {
                CurrentState = brief.CurrentState with
                {
                    SpendSummary = brief.CurrentState.SpendSummary with
                    {
                        TopCategories = brief.CurrentState.SpendSummary.TopCategories.Take(3).ToList()
                    }
                }
            };
            if (EstimateTokens(brief) <= tokenBudget) return brief;
        }

        // Truncation pass 5: keep AI interpretation, but trim secondary arrays.
        if (brief.CustomerInsightAiInterpretation is not null)
        {
            brief = brief with
            {
                CustomerInsightAiInterpretation = brief.CustomerInsightAiInterpretation with
                {
                    KeyObservations = brief.CustomerInsightAiInterpretation.KeyObservations.Take(3).ToList(),
                    RecommendedFocusAreas = brief.CustomerInsightAiInterpretation.RecommendedFocusAreas.Take(3).ToList(),
                    ReferencedMetricKeys = brief.CustomerInsightAiInterpretation.ReferencedMetricKeys.Take(5).ToList(),
                    Caveats = brief.CustomerInsightAiInterpretation.Caveats.Take(3).ToList()
                }
            };
            if (EstimateTokens(brief) <= tokenBudget) return brief;
        }

        // Truncation pass 6: trim lower-priority lists inside deterministic snapshot summary.
        if (brief.CustomerInsightSnapshot is not null)
        {
            brief = brief with
            {
                CustomerInsightSnapshot = brief.CustomerInsightSnapshot with
                {
                    CoverageWarnings = brief.CustomerInsightSnapshot.CoverageWarnings.Take(3).ToList(),
                    TopCategorySpend = brief.CustomerInsightSnapshot.TopCategorySpend.Take(3).ToList(),
                    TopMerchantSpend = brief.CustomerInsightSnapshot.TopMerchantSpend.Take(3).ToList(),
                    ObligationCoverageSummaries = brief.CustomerInsightSnapshot.ObligationCoverageSummaries.Take(3).ToList(),
                    BudgetPressureCategories = brief.CustomerInsightSnapshot.BudgetPressureCategories.Take(3).ToList(),
                    GoalProgressHighlights = brief.CustomerInsightSnapshot.GoalProgressHighlights.Take(3).ToList(),
                    KeyBehaviourSignals = brief.CustomerInsightSnapshot.KeyBehaviourSignals.Take(3).ToList(),
                    RiskFlags = brief.CustomerInsightSnapshot.RiskFlags.Take(3).ToList()
                }
            };
        }

        return brief;
    }

    private static int EstimateTokens(UserBrief brief) =>
        JsonSerializer.Serialize(brief).Length / 4;

    private static string? TryUnquote(string? json)
    {
        if (json is null) return null;
        try { return JsonSerializer.Deserialize<string>(json); }
        catch { return json; }
    }

    private static IReadOnlyList<string> ParseJsonArray(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }

    private static IReadOnlyList<T> ParseJsonList<T>(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        try { return JsonSerializer.Deserialize<List<T>>(json) ?? []; }
        catch { return []; }
    }
}
