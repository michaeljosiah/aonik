using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.PersonalFinance;

/// <summary>
/// First-class goal management for AONIK Compass (Spec 021 §3). Owns goal CRUD
/// plus the Compass programme metadata. Anemic <c>Goal</c> entity in, DTO out.
/// </summary>
internal sealed class GoalService : IGoalService
{
    private const string DefaultStatus = "Active";

    private readonly PersonalFinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public GoalService(
        PersonalFinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<GoalResponse> CreateGoalAsync(
        CreateGoalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Goal name is required.", nameof(request));
        }

        if (request.TargetAmount <= 0)
        {
            throw new ArgumentException("Goal target amount must be positive.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            throw new ArgumentException("Goal currency is required.", nameof(request));
        }

        var goal = new Goal
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantProvider.GetCurrentTenantId(),
            UserId = GetCurrentUserId(),
            Name = request.Name.Trim(),
            TargetAmount = request.TargetAmount,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            TargetDate = request.TargetDate,
            ProgressAmount = request.ProgressAmount,
            Status = DefaultStatus,
            FundingAccountId = request.FundingAccountId,
            GoalType = Normalize(request.GoalType),
            Strategy = Normalize(request.Strategy),
            RiskAppetite = Normalize(request.RiskAppetite),
            Priority = request.Priority,
            MilestonesJson = Normalize(request.MilestonesJson),
        };

        _dbContext.Goals.Add(goal);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(goal);
    }

    public async Task<GoalResponse> UpdateGoalAsync(
        Guid goalId,
        UpdateGoalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var goal = await LoadGoalAsync(goalId, cancellationToken)
            ?? throw new InvalidOperationException($"Goal {goalId} not found.");

        if (request.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("Goal name cannot be blank.", nameof(request));
            }

            goal.Name = request.Name.Trim();
        }

        if (request.TargetAmount.HasValue)
        {
            if (request.TargetAmount.Value <= 0)
            {
                throw new ArgumentException("Goal target amount must be positive.", nameof(request));
            }

            goal.TargetAmount = request.TargetAmount.Value;
        }

        if (request.Currency is not null)
        {
            goal.Currency = request.Currency.Trim().ToUpperInvariant();
        }

        if (request.TargetDate.HasValue)
        {
            goal.TargetDate = request.TargetDate;
        }

        if (request.ProgressAmount.HasValue)
        {
            goal.ProgressAmount = request.ProgressAmount.Value;
        }

        if (request.FundingAccountId.HasValue)
        {
            goal.FundingAccountId = request.FundingAccountId;
        }

        if (request.Status is not null)
        {
            goal.Status = request.Status.Trim();
        }

        if (request.GoalType is not null)
        {
            goal.GoalType = Normalize(request.GoalType);
        }

        if (request.Strategy is not null)
        {
            goal.Strategy = Normalize(request.Strategy);
        }

        if (request.RiskAppetite is not null)
        {
            goal.RiskAppetite = Normalize(request.RiskAppetite);
        }

        if (request.Priority.HasValue)
        {
            goal.Priority = request.Priority;
        }

        if (request.MilestonesJson is not null)
        {
            goal.MilestonesJson = Normalize(request.MilestonesJson);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(goal);
    }

    public async Task<GoalResponse?> GetGoalAsync(
        Guid goalId,
        CancellationToken cancellationToken = default)
    {
        var goal = await LoadGoalAsync(goalId, cancellationToken, asNoTracking: true);
        return goal is null ? null : Map(goal);
    }

    public async Task<IReadOnlyList<GoalResponse>> ListGoalsAsync(
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        var query = _dbContext.Goals
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim();
            query = query.Where(item => item.Status == normalized);
        }

        var goals = await query
            .OrderBy(item => item.Priority ?? int.MaxValue)
            .ThenByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return goals.Select(Map).ToList();
    }

    private async Task<Goal?> LoadGoalAsync(
        Guid goalId,
        CancellationToken cancellationToken,
        bool asNoTracking = false)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        IQueryable<Goal> query = _dbContext.Goals;
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(
            item => item.Id == goalId && item.TenantId == tenantId && item.UserId == userId,
            cancellationToken);
    }

    private Guid GetCurrentUserId()
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        return userId;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static GoalResponse Map(Goal goal)
    {
        var progressPercent = goal.TargetAmount > 0
            ? Math.Round(goal.ProgressAmount / goal.TargetAmount * 100m, 2)
            : 0m;

        return new GoalResponse(
            GoalId: goal.Id,
            UserId: goal.UserId,
            Name: goal.Name,
            TargetAmount: goal.TargetAmount,
            Currency: goal.Currency,
            TargetDate: goal.TargetDate,
            ProgressAmount: goal.ProgressAmount,
            ProgressPercent: progressPercent,
            Status: goal.Status,
            FundingAccountId: goal.FundingAccountId,
            GoalType: goal.GoalType,
            Strategy: goal.Strategy,
            RiskAppetite: goal.RiskAppetite,
            Priority: goal.Priority,
            MilestonesJson: goal.MilestonesJson,
            ActivePlanId: goal.ActivePlanId,
            CreatedAt: goal.CreatedAt,
            UpdatedAt: goal.UpdatedAt);
    }
}
