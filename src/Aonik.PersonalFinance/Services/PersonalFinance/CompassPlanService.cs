using System.Text.Json;
using Aonik.PersonalFinance.Agents.StructuredOutputs;
using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.PersonalFinance.Services;

/// <summary>
/// Owns the AONIK Compass plan lifecycle (Spec 021 §3). Generates a grounded,
/// versioned plan for a goal via the <c>pf-compass-planner</c> sub-agent,
/// supersedes prior plans, links the plan to the goal + snapshot, and records
/// an <c>AiRun</c> for every generation (RQ8). The deterministic figures handed
/// to the planner come from <see cref="ICompassGuidanceService"/> — the LLM
/// never computes the money number.
/// </summary>
internal sealed class CompassPlanService : ICompassPlanService
{
    private const int PlanHorizonDays = 90;
    private const string PlanGenerationUseCase = "compass-plan-generation";

    private readonly PersonalFinanceDbContext _dbContext;
    private readonly IGoalService _goalService;
    private readonly ICompassGuidanceService _guidanceService;
    private readonly ICompassPlanGenerator _planGenerator;
    private readonly ICustomerInsightSnapshotReader _snapshotReader;
    private readonly IAiRunWriter _aiRunWriter;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public CompassPlanService(
        PersonalFinanceDbContext dbContext,
        IGoalService goalService,
        ICompassGuidanceService guidanceService,
        ICompassPlanGenerator planGenerator,
        ICustomerInsightSnapshotReader snapshotReader,
        IAiRunWriter aiRunWriter,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _dbContext = dbContext;
        _goalService = goalService;
        _guidanceService = guidanceService;
        _planGenerator = planGenerator;
        _snapshotReader = snapshotReader;
        _aiRunWriter = aiRunWriter;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<CompassPlanResponse> GeneratePlanAsync(
        Guid goalId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        var goal = await _goalService.GetGoalAsync(goalId, cancellationToken)
            ?? throw new InvalidOperationException($"Goal {goalId} not found.");

        var horizonStart = DateTime.UtcNow;
        var horizonEnd = goal.TargetDate is { } target && target > horizonStart
            ? target
            : horizonStart.AddDays(PlanHorizonDays);

        // Deterministic grounding context for the planner (the safe-to-spend
        // number is computed here, not by the LLM).
        var safeToSpend = await _guidanceService.GetSafeToSpendAsync(horizonStart, cancellationToken);
        var snapshot = await _snapshotReader.GetCurrentSnapshotAsync(userId, cancellationToken);

        var inputRefs = JsonSerializer.Serialize(new
        {
            goalId,
            snapshotId = snapshot?.Id,
            safeToSpend = safeToSpend.SafeToSpend,
            currency = safeToSpend.Currency,
        });

        // Audit trail: start an AiRun before the LLM call; mark failed on error.
        var aiRunId = await _aiRunWriter.StartRunAsync(PlanGenerationUseCase, inputRefs, cancellationToken);

        CompassPlannerAgentToolResponse generated;
        try
        {
            var request = BuildPlannerRequest(goal, horizonStart, horizonEnd, safeToSpend);
            generated = await _planGenerator.GenerateAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            await _aiRunWriter.MarkRunFailedAsync(aiRunId, ex.Message, cancellationToken);
            throw;
        }

        // Supersede the current active plan and bump the version.
        var currentPlans = await _dbContext.CompassPlans
            .Where(p => p.TenantId == tenantId && p.UserId == userId
                        && p.GoalId == goalId && p.Status == CompassPlanStatus.Active)
            .ToListAsync(cancellationToken);

        var maxVersion = await _dbContext.CompassPlans
            .Where(p => p.TenantId == tenantId && p.UserId == userId && p.GoalId == goalId)
            .Select(p => (int?)p.Version)
            .MaxAsync(cancellationToken) ?? 0;

        var plan = new CompassPlan
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            GoalId = goalId,
            Version = maxVersion + 1,
            Status = CompassPlanStatus.Active,
            PlanJson = generated.PlanJson,
            HorizonStartUtc = horizonStart,
            HorizonEndUtc = horizonEnd,
            SnapshotId = snapshot?.Id,
            AiRunId = aiRunId,
        };

        foreach (var prior in currentPlans)
        {
            prior.Status = CompassPlanStatus.Superseded;
            prior.SupersededById = plan.Id;
        }

        _dbContext.CompassPlans.Add(plan);

        // Point the goal at its new active plan.
        var goalEntity = await _dbContext.Goals
            .FirstOrDefaultAsync(g => g.Id == goalId && g.TenantId == tenantId && g.UserId == userId, cancellationToken);
        if (goalEntity is not null)
        {
            goalEntity.ActivePlanId = plan.Id;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _aiRunWriter.MarkRunCompletedAsync(aiRunId, plan.Id.ToString(), cancellationToken);

        return CompassPlanMapper.Map(plan);
    }

    public async Task<CompassPlanResponse?> GetCurrentPlanAsync(
        Guid goalId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        var plan = await _dbContext.CompassPlans
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.UserId == userId
                        && p.GoalId == goalId && p.Status == CompassPlanStatus.Active)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(cancellationToken);

        return plan is null ? null : CompassPlanMapper.Map(plan);
    }

    public async Task<IReadOnlyList<CompassPlanResponse>> GetPlanHistoryAsync(
        Guid goalId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        var plans = await _dbContext.CompassPlans
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.UserId == userId && p.GoalId == goalId)
            .OrderByDescending(p => p.Version)
            .ToListAsync(cancellationToken);

        return plans.Select(CompassPlanMapper.Map).ToList();
    }

    public async Task<CompassPlanResponse> SupersedePlanAsync(
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        var plan = await _dbContext.CompassPlans
            .FirstOrDefaultAsync(p => p.Id == planId && p.TenantId == tenantId && p.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException($"Compass plan {planId} not found.");

        if (plan.Status == CompassPlanStatus.Active)
        {
            plan.Status = CompassPlanStatus.Superseded;

            // Clear the goal's active-plan pointer if it referenced this plan.
            var goal = await _dbContext.Goals
                .FirstOrDefaultAsync(g => g.Id == plan.GoalId && g.TenantId == tenantId && g.UserId == userId, cancellationToken);
            if (goal is not null && goal.ActivePlanId == plan.Id)
            {
                goal.ActivePlanId = null;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return CompassPlanMapper.Map(plan);
    }

    private static CompassPlannerRequest BuildPlannerRequest(
        GoalResponse goal,
        DateTime horizonStart,
        DateTime horizonEnd,
        SafeToSpendResponse safeToSpend)
    {
        var obligationLabels = safeToSpend.Factors
            .Select(f => $"{f.Label} ({f.Amount} {f.Currency})")
            .Take(20)
            .ToList();

        var context = new CompassPlannerContext(
            SafeToSpend: safeToSpend.SafeToSpend,
            LiquidAssets: safeToSpend.LiquidAssets,
            ProtectedObligations: safeToSpend.ProtectedObligations,
            OperatingCurrency: safeToSpend.Currency,
            GuidanceIsPartial: safeToSpend.IsPartial,
            ObligationLabels: obligationLabels,
            Warnings: safeToSpend.Warnings);

        return new CompassPlannerRequest(
            GoalId: goal.GoalId,
            GoalName: goal.Name,
            GoalType: goal.GoalType,
            TargetAmount: goal.TargetAmount,
            ProgressAmount: goal.ProgressAmount,
            Currency: goal.Currency,
            TargetDate: goal.TargetDate,
            RiskAppetite: goal.RiskAppetite,
            Strategy: goal.Strategy,
            HorizonStartUtc: horizonStart,
            HorizonEndUtc: horizonEnd,
            Context: context);
    }

    private Guid GetCurrentUserId()
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        return userId;
    }
}
