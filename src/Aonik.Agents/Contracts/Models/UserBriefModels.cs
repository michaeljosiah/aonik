namespace Aonik.Agents.Contracts.Models;

// ── User Brief Output Schema ─────────────────────────────────────────────

/// <summary>
/// The assembled user brief — a compact JSON payload projected from existing
/// domain data for consumption by AI agent sessions.
/// </summary>
public record UserBrief(
    UserBriefProfile UserProfile,
    UserBriefFinancialFocus FinancialFocus,
    UserBriefCurrentState CurrentState,
    CashflowRisk CashflowRisk,
    IReadOnlyList<UserBriefBehaviouralInsight> BehaviouralInsights,
    IReadOnlyList<UserBriefConversationMemory> RecentConversationMemory,
    UserBriefPolicyContext PolicyContext,
    DateTimeOffset GeneratedAt);

public record UserBriefProfile(
    string? PreferredName,
    string? CommunicationStyle,
    string? FinancialPosture,
    IReadOnlyList<string> CorridorCountries,
    string? HouseholdContext,
    string? IncomeRhythm,
    IReadOnlyList<string> PrimaryNeeds);

public record UserBriefFinancialFocus(
    IReadOnlyList<UserBriefGoal> CurrentGoals,
    IReadOnlyList<UserBriefObligation> SupportObligations);

public record UserBriefGoal(
    Guid GoalId,
    string Name,
    decimal TargetAmount,
    decimal ProgressAmount,
    string Currency,
    DateTime? TargetDate,
    string Status);

public record UserBriefObligation(
    string DisplayName,
    decimal? Amount,
    string Currency,
    string? Frequency,
    DateTime? NextDueDate);

public record UserBriefCurrentState(
    UserBriefCashSummary CashSummary,
    IReadOnlyList<UserBriefBill> NextBills,
    IReadOnlyList<UserBriefSubscription> Subscriptions,
    UserBriefSpendSummary? SpendSummary,
    IReadOnlyList<UserBriefBudgetPressure> BudgetPressureCategories);

public record UserBriefCashSummary(
    decimal TotalBalance,
    decimal AvailableBalance,
    string Currency);

public record UserBriefBill(
    Guid BillId,
    string Payee,
    decimal? Amount,
    string Currency,
    DateTime DueDate,
    bool Autopay);

public record UserBriefSubscription(
    Guid SubscriptionId,
    string Merchant,
    decimal ExpectedAmount,
    string Currency,
    DateTime RenewalDate);

public record UserBriefSpendSummary(
    decimal TotalSpend,
    IReadOnlyList<UserBriefCategorySpend> TopCategories,
    DateTime PeriodStart,
    DateTime PeriodEnd);

public record UserBriefCategorySpend(
    string Category,
    decimal Amount,
    decimal Percentage);

public record UserBriefBudgetPressure(
    string Category,
    decimal Budgeted,
    decimal Actual,
    decimal PercentUsed);

public enum CashflowRisk
{
    Low = 1,
    Moderate = 2,
    High = 3
}

public record UserBriefBehaviouralInsight(
    string InsightType,
    string Title,
    string Summary,
    decimal Confidence);

public record UserBriefConversationMemory(
    DateTime SessionDate,
    string Summary,
    IReadOnlyList<UserBriefOpenLoop> OpenLoops,
    IReadOnlyList<UserBriefRecommendationOutcome> RecommendationOutcomes);

public record UserBriefOpenLoop(
    string Description,
    string? Priority,
    string? DueDate);

public record UserBriefRecommendationOutcome(
    string? RecommendationId,
    string Outcome,
    string? Reason);

public record UserBriefPolicyContext(
    string RiskTier,
    IReadOnlyList<string> AiCanDo,
    IReadOnlyList<string> AiCannotDoWithoutApproval);

// ── Projector Options ────────────────────────────────────────────────────

public record UserBriefOptions
{
    /// <summary>How many days of bills to include in the lookahead.</summary>
    public int BillLookaheadDays { get; init; } = 14;

    /// <summary>How many recent conversation summaries to include.</summary>
    public int ConversationHistoryDepth { get; init; } = 3;

    /// <summary>Maximum number of behavioural insights to include.</summary>
    public int MaxBehaviouralInsights { get; init; } = 5;

    /// <summary>Whether to include full account balances or just totals.</summary>
    public bool IncludeAccountDetail { get; init; } = true;

    /// <summary>Spending summary period. Defaults to current calendar month.</summary>
    public DateTime? SpendPeriodStart { get; init; }
    public DateTime? SpendPeriodEnd { get; init; }

    /// <summary>Approximate token budget for the assembled brief.</summary>
    public int TokenBudget { get; init; } = 2000;
}
