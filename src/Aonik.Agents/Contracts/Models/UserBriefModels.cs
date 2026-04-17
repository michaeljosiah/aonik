namespace Aonik.Agents.Contracts.Models;

// ── User Brief Output Schema ─────────────────────────────────────────────

/// <summary>
/// The assembled user brief — a compact JSON payload projected from existing
/// domain data for consumption by AI agent sessions. Deliberately slim: each
/// field maps to something the LLM actually reads.
/// </summary>
public record UserBrief(
    DateTimeOffset AsOf,
    UserBriefUser User,
    IReadOnlyList<string> Goals,
    UserBriefCash? Cash,
    UserBriefPeriod? Period,
    IReadOnlyList<UserBriefAmount> TopCategories,
    IReadOnlyList<UserBriefAmount> TopMerchants,
    IReadOnlyList<UserBriefSignal> Signals,
    IReadOnlyList<string> Risks,
    CashflowRisk CashflowRisk,
    IReadOnlyList<string> MissingData,
    IReadOnlyList<string> AiCanDo,
    IReadOnlyList<string> AiNeedsApproval);

public record UserBriefUser(string? Name, string? Country);

public record UserBriefCash(decimal Balance, string Currency);

public record UserBriefPeriod(decimal Inflows, decimal Outflows, string Currency);

public record UserBriefAmount(string Name, decimal Amount);

public record UserBriefSignal(string Title, string Severity);

public enum CashflowRisk
{
    Low = 1,
    Moderate = 2,
    High = 3
}

// ── Projector Options ────────────────────────────────────────────────────

public record UserBriefOptions
{
    /// <summary>How many days of bills to include when sourcing financial data.</summary>
    public int BillLookaheadDays { get; init; } = 14;

    /// <summary>Spending summary period. Defaults to current calendar month.</summary>
    public DateTime? SpendPeriodStart { get; init; }
    public DateTime? SpendPeriodEnd { get; init; }
}
