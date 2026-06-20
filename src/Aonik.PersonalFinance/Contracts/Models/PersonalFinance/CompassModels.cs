namespace Aonik.Finance.Contracts.Models.PersonalFinance;

// ════════════════════════════════════════════════════════════════════
// AONIK Compass — DTOs (Spec 021)
//
// Goal programme management, persisted plan lifecycle, deterministic
// safe-to-spend guidance, and Compass proposal creation/retrieval. All
// records (anemic-friendly) returned by the Compass services; entities are
// never returned across the service boundary.
// ════════════════════════════════════════════════════════════════════

// ── Goal programme DTOs (IGoalService) ──────────────────────────────

public record CreateGoalRequest(
    string Name,
    decimal TargetAmount,
    string Currency,
    DateTime? TargetDate = null,
    decimal ProgressAmount = 0m,
    Guid? FundingAccountId = null,
    string? GoalType = null,
    string? Strategy = null,
    string? RiskAppetite = null,
    int? Priority = null,
    string? MilestonesJson = null);

public record UpdateGoalRequest(
    string? Name = null,
    decimal? TargetAmount = null,
    string? Currency = null,
    DateTime? TargetDate = null,
    decimal? ProgressAmount = null,
    Guid? FundingAccountId = null,
    string? Status = null,
    string? GoalType = null,
    string? Strategy = null,
    string? RiskAppetite = null,
    int? Priority = null,
    string? MilestonesJson = null);

public record GoalResponse(
    Guid GoalId,
    Guid UserId,
    string Name,
    decimal TargetAmount,
    string Currency,
    DateTime? TargetDate,
    decimal ProgressAmount,
    decimal ProgressPercent,
    string Status,
    Guid? FundingAccountId,
    string? GoalType,
    string? Strategy,
    string? RiskAppetite,
    int? Priority,
    string? MilestonesJson,
    Guid? ActivePlanId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

// ── Plan DTOs (ICompassPlanService) ─────────────────────────────────

public record CompassPlanResponse(
    Guid PlanId,
    Guid GoalId,
    Guid UserId,
    int Version,
    string Status,
    string PlanJson,
    DateTime HorizonStartUtc,
    DateTime HorizonEndUtc,
    Guid? SnapshotId,
    Guid? AiRunId,
    Guid? SupersededById,
    DateTime CreatedAt);

// ── Safe-to-spend / guidance DTOs (ICompassGuidanceService) ─────────

/// <summary>
/// One protected obligation (or plan commitment) deducted from liquid
/// assets when computing safe-to-spend. Mirrors the dashboard's factor
/// shape so the two surfaces read consistently.
/// </summary>
public record SafeToSpendFactor(
    string Kind,
    Guid? SourceId,
    string Label,
    decimal Amount,
    string Currency,
    DateTime? DueDate);

/// <summary>
/// Deterministic safe-to-spend result. When <see cref="IsPartial"/> is true
/// (missing snapshot, insufficient data, or a mixed-currency user) the
/// service returns warnings instead of a fabricated blended amount — the
/// V1 currency rule (Spec 021 §3, DEC8).
/// </summary>
public record SafeToSpendResponse(
    decimal LiquidAssets,
    decimal ProtectedObligations,
    decimal PlanCommitments,
    decimal SafeToSpend,
    string Currency,
    DateTime AsOfUtc,
    int LookaheadDays,
    bool IsPartial,
    IReadOnlyList<SafeToSpendFactor> Factors,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Guidance for a single goal: the goal, its current plan summary, the
/// user's current safe-to-spend, and any warnings.
/// </summary>
public record GoalGuidanceResponse(
    GoalResponse Goal,
    CompassPlanResponse? CurrentPlan,
    SafeToSpendResponse SafeToSpend,
    IReadOnlyList<string> Warnings);

// ── Compass proposal DTOs ───────────────────────────────────────────

public record CreateCompassProposalRequest(
    Guid GoalId,
    string ActionType,
    decimal Amount,
    string Currency,
    string Rationale,
    string RiskTier = "low",
    Guid? PlanId = null);

public record CompassProposalResponse(
    Guid ProposalId,
    Guid GoalId,
    Guid? PlanId,
    string ActionType,
    decimal Amount,
    string Currency,
    string RiskTier,
    string Status,
    string Rationale);
