using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Contracts.Services.PersonalFinance;

/// <summary>
/// Deterministic AONIK Compass guidance (Spec 021 §3). Computes safe-to-spend
/// from existing balances, obligations, and active plan commitments — no LLM
/// for the number, no new persistence table — and turns guidance into Compass
/// recommendations reusing the existing <c>Proposal</c> system.
/// </summary>
public interface ICompassGuidanceService
{
    /// <summary>
    /// Deterministic safe-to-spend for the current user as of <paramref name="asOfDate"/>.
    /// Single-currency only (DEC8): mixed-currency or insufficient-data users get
    /// warning-based partial guidance rather than a fabricated blended amount.
    /// Falls back to on-demand snapshot generation when none exists (DEC9).
    /// </summary>
    Task<SafeToSpendResponse> GetSafeToSpendAsync(
        DateTime asOfDate,
        CancellationToken cancellationToken = default);

    Task<GoalGuidanceResponse> GetGoalGuidanceAsync(
        Guid goalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Compass recommendation as a <c>Proposal</c> (ProposalType
    /// <c>CompassRecommendation</c>) carrying userId/goalId/planId linkage in
    /// its payload so current-user retrieval needs no free-text scanning.
    /// </summary>
    Task<CompassProposalResponse> CreateCompassProposalAsync(
        CreateCompassProposalRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Lists pending Compass proposals for the current user (tenant + type + payload linkage).</summary>
    Task<IReadOnlyList<CompassProposalResponse>> ListCompassProposalsAsync(
        CancellationToken cancellationToken = default);
}
