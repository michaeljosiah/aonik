using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Contracts.Services.PersonalFinance;

/// <summary>
/// Owns the AONIK Compass plan lifecycle (Spec 021 §3): generating a grounded,
/// versioned plan for a goal via the <c>pf-compass-planner</c> sub-agent,
/// retrieving the current plan and history, and superseding plans. Every
/// generation is recorded as an <c>AiRun</c> (RQ8).
/// </summary>
public interface ICompassPlanService
{
    /// <summary>
    /// Generates a new plan for the goal. Supersedes any current plan, bumps
    /// the version, grounds on the latest <c>CustomerInsightSnapshot</c> (or an
    /// on-demand one), and records an <c>AiRun</c>.
    /// </summary>
    Task<CompassPlanResponse> GeneratePlanAsync(
        Guid goalId,
        CancellationToken cancellationToken = default);

    Task<CompassPlanResponse?> GetCurrentPlanAsync(
        Guid goalId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CompassPlanResponse>> GetPlanHistoryAsync(
        Guid goalId,
        CancellationToken cancellationToken = default);

    /// <summary>Marks the given active plan as Superseded. Idempotent for non-active plans.</summary>
    Task<CompassPlanResponse> SupersedePlanAsync(
        Guid planId,
        CancellationToken cancellationToken = default);
}
