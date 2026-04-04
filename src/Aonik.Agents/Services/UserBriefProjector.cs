using System.Text.Json;
using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.PersonalFinance;
using Aonik.SharedKernel.Abstractions.UserBrief;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Services;

internal sealed class UserBriefProjector : IUserBriefProjector
{
    private readonly IUserBriefDataProvider _financeData;
    private readonly IUserBriefAiDataProvider _aiData;
    private readonly IUserBriefContextDataProvider _userContextData;
    private readonly AgentsDbContext _agentsDbContext;
    private readonly ILogger<UserBriefProjector> _logger;

    public UserBriefProjector(
        IUserBriefDataProvider financeData,
        IUserBriefAiDataProvider aiData,
        IUserBriefContextDataProvider userContextData,
        AgentsDbContext agentsDbContext,
        ILogger<UserBriefProjector> logger)
    {
        _financeData = financeData;
        _aiData = aiData;
        _userContextData = userContextData;
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
        // Note: _aiData methods share a DbContext so they must run sequentially.
        // Run them together in one task, concurrent with the other providers.
        var financeRequest = new UserBriefFinancialDataRequest(
            options.BillLookaheadDays,
            options.SpendPeriodStart,
            options.SpendPeriodEnd);

        var financeTask = _financeData.GetFinancialDataAsync(tenantId, userId, financeRequest, cancellationToken);
        var aiTask = GetAiDataAsync(tenantId, userId, options, cancellationToken);
        var userContextTask = _userContextData.GetUserContextDataAsync(tenantId, userId, cancellationToken);

        await Task.WhenAll(financeTask, aiTask, userContextTask);

        var financeData = await financeTask;
        var (insights, memoryEntries) = await aiTask;
        var userContextData = await userContextTask;
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
        var userProfile = AssembleUserProfile(userContextData, memoryEntries, financeData);
        var setupProfile = AssembleSetupProfile(userContextData.SetupProfile);
        var financialFocus = AssembleFinancialFocus(financeData);
        var currentState = AssembleCurrentState(financeData);
        var customerInsightSnapshot = AssembleCustomerInsightSnapshot(financeData.CustomerInsightSnapshot);
        var customerInsightAiInterpretation = AssembleCustomerInsightAiInterpretation(
            customerInsightSummary,
            financeData.CustomerInsightSnapshot);
        var dataAvailability = DeriveDataAvailability(
            userContextData,
            financeData,
            memoryEntries,
            conversationSummaries);
        var cashflowRisk = DeriveCashflowRisk(financeData);
        var behaviouralInsights = AssembleBehaviouralInsights(financeData.CustomerInsightSnapshot, insights);
        var conversationMemory = AssembleConversationMemory(conversationSummaries);
        var policyContext = DerivePolicyContext(memoryEntries);

        var brief = new UserBrief(
            userProfile,
            setupProfile,
            financialFocus,
            currentState,
            customerInsightSnapshot,
            customerInsightAiInterpretation,
            dataAvailability,
            cashflowRisk,
            behaviouralInsights,
            conversationMemory,
            policyContext,
            DateTimeOffset.UtcNow);

        // ── Token budget enforcement ───────────────────────────────────
        return ApplyTokenBudget(brief, options.TokenBudget);
    }

    private async Task<(IReadOnlyList<UserBriefInsightData> Insights, IReadOnlyList<UserBriefMemoryEntryData> MemoryEntries)> GetAiDataAsync(
        Guid tenantId,
        Guid userId,
        UserBriefOptions options,
        CancellationToken cancellationToken)
    {
        var insights = await _aiData.GetBehaviouralInsightsAsync(tenantId, userId, options.MaxBehaviouralInsights, cancellationToken);
        var memoryEntries = await _aiData.GetCurrentMemoryEntriesAsync(tenantId, userId, cancellationToken);
        return (insights, memoryEntries);
    }

    private static UserBriefProfile AssembleUserProfile(
        UserBriefContextData userContextData,
        IReadOnlyList<UserBriefMemoryEntryData> memoryEntries,
        UserBriefFinancialData financeData)
    {
        string? GetMemoryValue(string key) =>
            memoryEntries.FirstOrDefault(e => e.Key == key)?.ValueJson;

        var fullName = FirstNonEmpty(
            userContextData.FullName,
            JoinName(userContextData.FirstName, userContextData.LastName));
        var givenName = FirstNonEmpty(
            userContextData.FirstName,
            fullName?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault(),
            userContextData.Email?.Split('@', 2, StringSplitOptions.TrimEntries).FirstOrDefault());
        var preferredName = FirstNonEmpty(
            TryUnquote(GetMemoryValue("identity.preferred_name")),
            givenName,
            fullName);

        return new UserBriefProfile(
            PreferredName: preferredName,
            FullName: fullName,
            GivenName: givenName,
            Email: userContextData.Email,
            PhoneNumber: userContextData.PhoneNumber,
            UserCreatedAt: userContextData.UserCreatedAt,
            CommunicationStyle: TryUnquote(GetMemoryValue("communication.style")),
            FinancialPosture: TryUnquote(GetMemoryValue("identity.financial_posture")),
            CorridorCountries: financeData.CorridorCountries,
            HouseholdContext: financeData.HouseholdContext
                ?? TryUnquote(GetMemoryValue("identity.household_context")),
            IncomeRhythm: TryUnquote(GetMemoryValue("fact.income_rhythm"))
                ?? GetMemoryValue("income.payday"),
            PrimaryNeeds: ParseJsonArray(GetMemoryValue("identity.primary_needs")));
    }

    private static UserBriefSetupProfile? AssembleSetupProfile(UserBriefSetupProfileData? setupProfile)
    {
        if (setupProfile is null)
        {
            return null;
        }

        return new UserBriefSetupProfile(
            setupProfile.SelectedUseCases,
            setupProfile.AccountSourceTypes,
            setupProfile.ConnectChoice,
            setupProfile.Responsibilities,
            setupProfile.SupportType,
            setupProfile.FinancialGoals,
            setupProfile.Completed);
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

        var spendSummaries = data.SpendSummaries.Select(s => new UserBriefSpendSummary(
            s.Currency,
            s.TotalSpend,
            s.TopCategories.Select(c => new UserBriefCategorySpend(
                c.Category, c.Amount, c.Percentage)).ToList(),
            s.PeriodStart,
            s.PeriodEnd)).ToList();

        var budgetPressure = data.BudgetPressure.Select(b => new UserBriefBudgetPressure(
            b.Category, b.Budgeted, b.Actual, b.PercentUsed)).ToList();

        return new UserBriefCurrentState(cashSummary, bills, subscriptions, spendSummaries, budgetPressure);
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
        UserBriefCustomerInsightSnapshotData? snapshot,
        IReadOnlyList<UserBriefInsightData> insights)
    {
        if (snapshot is not null && snapshot.KeyBehaviourSignals.Count > 0)
        {
            return snapshot.KeyBehaviourSignals
                .Select(signal => new UserBriefBehaviouralInsight(
                    signal.Category,
                    signal.Title,
                    signal.Description,
                    MapSignalConfidence(signal.Confidence)))
                .ToList();
        }

        return insights.Select(i => new UserBriefBehaviouralInsight(
            i.InsightType, i.Title, i.Summary, i.Confidence)).ToList();
    }

    private static decimal MapSignalConfidence(string confidence) => confidence switch
    {
        "High" => 0.9m,
        "Medium" => 0.7m,
        _ => 0.5m
    };

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

    private static UserBriefDataAvailability DeriveDataAvailability(
        UserBriefContextData userContextData,
        UserBriefFinancialData financeData,
        IReadOnlyList<UserBriefMemoryEntryData> memoryEntries,
        List<ConversationSummary> conversationSummaries)
    {
        var missingDataAreas = new List<string>();

        if (financeData.AccountCount == 0)
        {
            missingDataAreas.Add("accounts");
        }

        if (financeData.TransactionCount == 0)
        {
            missingDataAreas.Add("transactions");
        }

        if (financeData.ActiveGoals.Count == 0)
        {
            missingDataAreas.Add("goals");
        }

        if (financeData.UpcomingBills.Count == 0 && financeData.ActiveSubscriptions.Count == 0)
        {
            missingDataAreas.Add("bills_and_subscriptions");
        }

        if (financeData.CustomerInsightSnapshot is null)
        {
            missingDataAreas.Add("customer_insight_snapshot");
        }

        if (conversationSummaries.Count == 0)
        {
            missingDataAreas.Add("conversation_history");
        }

        var isNewUser = financeData.AccountCount == 0
            && financeData.TransactionCount == 0
            && financeData.ActiveGoals.Count == 0
            && financeData.SupportObligations.Count == 0
            && financeData.CustomerInsightSnapshot is null
            && memoryEntries.Count == 0
            && conversationSummaries.Count == 0;

        var hasLimitedFinancialData = isNewUser
            || financeData.TransactionCount < 5
            || financeData.CustomerInsightSnapshot is null;

        var summary = isNewUser
            ? userContextData.SetupProfile is null
                ? "This is a new Payabo user with little or no financial history yet. Be explicit that no meaningful behavioural or spending pattern can be inferred yet. Focus on onboarding and next-step guidance."
                : "This is a new Payabo user with little or no financial history yet. Use the setup answers as the main context, avoid over-interpreting patterns, and focus on onboarding and next-step guidance."
            : hasLimitedFinancialData
                ? "Only limited financial data is available. Keep guidance cautious, state when conclusions are tentative, and avoid claiming strong patterns."
                : "Sufficient recent data is available for normal personal-finance guidance.";

        return new UserBriefDataAvailability(
            IsNewUser: isNewUser,
            HasLimitedFinancialData: hasLimitedFinancialData,
            Summary: summary,
            MissingDataAreas: missingDataAreas);
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

        // Truncation pass 4: truncate spend categories to top 3 per currency
        if (brief.CurrentState.SpendSummaries.Any(s => s.TopCategories.Count > 3))
        {
            brief = brief with
            {
                CurrentState = brief.CurrentState with
                {
                    SpendSummaries = brief.CurrentState.SpendSummaries
                        .Select(s => s with { TopCategories = s.TopCategories.Take(3).ToList() })
                        .ToList()
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

    private static string? JoinName(string? firstName, string? lastName)
    {
        var parts = new[] { firstName?.Trim(), lastName?.Trim() }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return parts.Length == 0 ? null : string.Join(' ', parts);
    }

    private static string? FirstNonEmpty(params string?[] candidates)
    {
        return candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate))?.Trim();
    }

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
