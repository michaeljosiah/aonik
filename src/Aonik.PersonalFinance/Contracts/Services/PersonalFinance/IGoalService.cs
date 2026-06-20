using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Contracts.Services.PersonalFinance;

/// <summary>
/// First-class goal management for AONIK Compass (Spec 021 §3). Compass needs
/// a supported goal API before it can plan against goals; this service owns
/// goal CRUD and the Compass programme metadata layered onto <c>Goal</c>.
/// Current user/tenant are resolved from <c>ICurrentUserProvider</c>/<c>ITenantProvider</c>.
/// </summary>
public interface IGoalService
{
    Task<GoalResponse> CreateGoalAsync(
        CreateGoalRequest request,
        CancellationToken cancellationToken = default);

    Task<GoalResponse> UpdateGoalAsync(
        Guid goalId,
        UpdateGoalRequest request,
        CancellationToken cancellationToken = default);

    Task<GoalResponse?> GetGoalAsync(
        Guid goalId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GoalResponse>> ListGoalsAsync(
        string? status = null,
        CancellationToken cancellationToken = default);
}
