using System.ComponentModel;
using System.Text.Json;
using Aonik.Finance.Agents.StructuredOutputs;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.Extensions.AI;

namespace Aonik.Finance.Agents.Tools;

/// <summary>
/// AONIK Compass (Spec 021) tools — goal programme management, plan
/// generation/retrieval, deterministic safe-to-spend, a read-only planner
/// preview, and Compass proposal creation. Read tools execute directly; mutating
/// tools are gated server-side (Spec 032) and follow the confirmAction
/// convention. Compass never moves money — recommendations are Proposals
/// reviewed by the user. Registered by <see cref="PersonalFinanceTools.CreateAll"/>.
/// </summary>
internal sealed class PersonalFinanceCompassTools : PersonalFinanceSubAgentToolGroup
{
    private readonly IGoalService _goalService;
    private readonly ICompassPlanService _compassPlanService;
    private readonly ICompassGuidanceService _compassGuidanceService;

    public PersonalFinanceCompassTools(
        IGoalService goalService,
        ICompassPlanService compassPlanService,
        ICompassGuidanceService compassGuidanceService,
        IChatClient chatClient,
        IServiceProvider serviceProvider,
        IAgentConfigurationService agentConfigurationService,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
        : base(chatClient, serviceProvider, agentConfigurationService, tenantProvider, currentUserProvider)
    {
        _goalService = goalService;
        _compassPlanService = compassPlanService;
        _compassGuidanceService = compassGuidanceService;
    }

    [Description("Lists the user's financial goals, including AONIK Compass programme metadata (goal type, strategy, risk appetite, priority, progress, and active plan id). Optionally filter by status (e.g. 'Active', 'Completed'). Use this for 'what am I saving for', 'show my goals', or before generating/retrieving a plan.")]
    public async Task<IReadOnlyList<GoalResponse>> ListGoals(
        [Description("Optional status filter (e.g. 'Active', 'Completed', 'Cancelled'). Null returns all.")] string? status = null,
        CancellationToken cancellationToken = default)
    {
        return await _goalService.ListGoalsAsync(status, cancellationToken);
    }

    [Description("Gets a single goal by id, including its Compass programme metadata and progress. Returns null if the goal does not belong to the current user.")]
    public async Task<GoalResponse?> GetGoal(
        [Description("The unique identifier (GUID) of the goal")] Guid goalId,
        CancellationToken cancellationToken = default)
    {
        return await _goalService.GetGoalAsync(goalId, cancellationToken);
    }

    [Description("Gets the current active AONIK Compass plan for a goal (narrative summary, recommended steps with suggested amounts/timing, rationale, and warnings, as structured plan JSON). Returns null if the goal has no plan yet — use pf_generate_goal_plan to create one.")]
    public async Task<CompassPlanResponse?> GetGoalPlan(
        [Description("The unique identifier (GUID) of the goal")] Guid goalId,
        CancellationToken cancellationToken = default)
    {
        return await _compassPlanService.GetCurrentPlanAsync(goalId, cancellationToken);
    }

    [Description("Gets the user's deterministic AONIK Compass safe-to-spend as of today: liquid assets minus protected upcoming obligations minus active plan commitments, with the per-item breakdown. Single-currency only — if the user's balances/obligations span multiple currencies, or data is insufficient, the result is marked partial (isPartial=true) with warnings instead of a blended number. Use this for 'how much can I spend', 'what's safe to spend today'.")]
    public async Task<SafeToSpendResponse> GetSafeToSpend(
        [Description("Optional reference date for the guidance (UTC). Null defaults to today UTC.")] DateTime? asOfDate = null,
        CancellationToken cancellationToken = default)
    {
        return await _compassGuidanceService.GetSafeToSpendAsync(asOfDate ?? DateTime.UtcNow, cancellationToken);
    }

    [Description("Runs the internal pf-compass-planner specialist to PREVIEW a structured plan for a goal without persisting it. The deterministic safe-to-spend context is computed server-side and handed to the planner — the plan it returns (narrative + steps + suggested amounts/timing + rationale + warnings) is a draft. To actually save a plan (and supersede the prior one), use pf_generate_goal_plan. Read-only; never moves money.")]
    public async Task<CompassPlannerAgentToolResponse> RunCompassPlanner(
        [Description("The unique identifier (GUID) of the goal to plan for")] Guid goalId,
        CancellationToken cancellationToken = default)
    {
        var goal = await _goalService.GetGoalAsync(goalId, cancellationToken)
            ?? throw new InvalidOperationException($"Goal {goalId} not found.");

        var horizonStart = DateTime.UtcNow;
        var horizonEnd = goal.TargetDate is { } target && target > horizonStart
            ? target
            : horizonStart.AddDays(90);

        var safeToSpend = await _compassGuidanceService.GetSafeToSpendAsync(horizonStart, cancellationToken);

        var context = new CompassPlannerContext(
            SafeToSpend: safeToSpend.SafeToSpend,
            LiquidAssets: safeToSpend.LiquidAssets,
            ProtectedObligations: safeToSpend.ProtectedObligations,
            OperatingCurrency: safeToSpend.Currency,
            GuidanceIsPartial: safeToSpend.IsPartial,
            ObligationLabels: safeToSpend.Factors.Select(f => $"{f.Label} ({f.Amount} {f.Currency})").Take(20).ToList(),
            Warnings: safeToSpend.Warnings);

        var request = new CompassPlannerRequest(
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

        var message = JsonSerializer.Serialize(request, CompassPlannerStructuredOutputContract.SerializerOptions);

        try
        {
            var descriptor = ResolveSubAgentDescriptor("pf-compass-planner");
            // pf-compass-planner does not implement ISubAgentDescriptor — it takes
            // its financial context as a request payload (Context above) and never
            // itself resolves the scoped user/tenant, so there is no impersonation
            // hazard to guard against here. Empty snapshot preserves its exact
            // pre-existing Build() path unchanged.
            var agent = await BuildStructuredSubAgentAsync(descriptor, SubAgentImpersonationSnapshot.Empty, cancellationToken);

            var response = await agent.RunAsync<CompassPlanResult>(
                message,
                session: null,
                serializerOptions: CompassPlannerStructuredOutputContract.SerializerOptions,
                options: null,
                cancellationToken: cancellationToken);

            var planJson = JsonSerializer.Serialize(response.Result, CompassPlannerStructuredOutputContract.SerializerOptions);
            return new CompassPlannerAgentToolResponse(response.Result, planJson);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSubAgentException("pf-compass-planner", $"goal:{goalId}", ex);
            return BuildCompassPlannerErrorResponse(ex);
        }
    }

    [Description("Creates a new AONIK Compass goal programme: a goal plus guidance metadata (goalType: 'cashflow' | 'savings' | 'debt_reduction' | 'purchase'; optional strategy, riskAppetite: 'conservative' | 'moderate' | 'aggressive'; priority; milestonesJson). Requires confirmAction approval. Does not move money.")]
    public async Task<GoalResponse> CreateGoalProgramme(
        [Description("Display name for the goal (e.g. 'Holiday fund')")] string name,
        [Description("Target amount to reach")] decimal targetAmount,
        [Description("ISO 4217 currency code (e.g. GBP, NGN)")] string currency,
        [Description("Optional target date (UTC) to reach the goal by")] DateTime? targetDate = null,
        [Description("Optional amount already saved toward the goal (default: 0)")] decimal progressAmount = 0,
        [Description("Optional Compass goal type: 'cashflow', 'savings', 'debt_reduction', 'purchase'")] string? goalType = null,
        [Description("Optional short strategy summary")] string? strategy = null,
        [Description("Optional risk appetite: 'conservative', 'moderate', 'aggressive'")] string? riskAppetite = null,
        [Description("Optional relative priority across goals (lower = higher priority)")] int? priority = null,
        [Description("Optional account ID to fund the goal from")] Guid? fundingAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateGoalRequest(
            Name: name,
            TargetAmount: targetAmount,
            Currency: currency,
            TargetDate: targetDate,
            ProgressAmount: progressAmount,
            FundingAccountId: fundingAccountId,
            GoalType: goalType,
            Strategy: strategy,
            RiskAppetite: riskAppetite,
            Priority: priority,
            MilestonesJson: null);
        return await _goalService.CreateGoalAsync(request, cancellationToken);
    }

    [Description("Updates an existing goal and/or its Compass programme metadata. Only the fields you provide change; unspecified fields keep their current values. Use this to adjust the target, rename, change status, set strategy/riskAppetite/priority, or record progress. Requires confirmAction approval. Does not move money.")]
    public async Task<GoalResponse> UpdateGoalProgramme(
        [Description("The unique identifier (GUID) of the goal to update")] Guid goalId,
        [Description("Optional new name")] string? name = null,
        [Description("Optional new target amount")] decimal? targetAmount = null,
        [Description("Optional new amount saved toward the goal")] decimal? progressAmount = null,
        [Description("Optional new target date (UTC)")] DateTime? targetDate = null,
        [Description("Optional new status (e.g. 'Active', 'Completed', 'Cancelled')")] string? status = null,
        [Description("Optional Compass goal type: 'cashflow', 'savings', 'debt_reduction', 'purchase'")] string? goalType = null,
        [Description("Optional short strategy summary")] string? strategy = null,
        [Description("Optional risk appetite: 'conservative', 'moderate', 'aggressive'")] string? riskAppetite = null,
        [Description("Optional relative priority across goals (lower = higher priority)")] int? priority = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateGoalRequest(
            Name: name,
            TargetAmount: targetAmount,
            Currency: null,
            TargetDate: targetDate,
            ProgressAmount: progressAmount,
            FundingAccountId: null,
            Status: status,
            GoalType: goalType,
            Strategy: strategy,
            RiskAppetite: riskAppetite,
            Priority: priority,
            MilestonesJson: null);
        return await _goalService.UpdateGoalAsync(goalId, request, cancellationToken);
    }

    [Description("Generates and SAVES a new AONIK Compass plan for a goal via the pf-compass-planner specialist. This supersedes any current plan, bumps the version, grounds the plan on the latest insight snapshot + deterministic safe-to-spend, links it to the goal, and records an AiRun. Requires confirmAction approval. Does not move money — the plan is a set of recommendations.")]
    public async Task<CompassPlanResponse> GenerateGoalPlan(
        [Description("The unique identifier (GUID) of the goal to (re)plan")] Guid goalId,
        CancellationToken cancellationToken = default)
    {
        return await _compassPlanService.GeneratePlanAsync(goalId, cancellationToken);
    }

    [Description("Creates an AONIK Compass recommendation as a reviewable Proposal for a goal (actionType e.g. 'savings_transfer', amount, currency, rationale). Compass never moves money directly — this records a proposal the user can approve later. Carries goal/plan linkage in its payload. Requires confirmAction approval.")]
    public async Task<CompassProposalResponse> CreateCompassProposal(
        [Description("The unique identifier (GUID) of the goal the recommendation is for")] Guid goalId,
        [Description("The recommended action type (e.g. 'savings_transfer', 'increase_contribution')")] string actionType,
        [Description("The recommended amount")] decimal amount,
        [Description("ISO 4217 currency code (e.g. GBP, NGN). Defaults to the goal's currency when blank.")] string currency,
        [Description("Plain-language rationale for the recommendation")] string rationale,
        [Description("Risk tier for the recommendation: 'low' (default), 'medium', or 'high'")] string riskTier = "low",
        [Description("Optional id of the Compass plan this recommendation came from")] Guid? planId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateCompassProposalRequest(
            GoalId: goalId,
            ActionType: actionType,
            Amount: amount,
            Currency: currency,
            Rationale: rationale,
            RiskTier: riskTier,
            PlanId: planId);
        return await _compassGuidanceService.CreateCompassProposalAsync(request, cancellationToken);
    }

    private static CompassPlannerAgentToolResponse BuildCompassPlannerErrorResponse(Exception ex)
    {
        var message = FormatExceptionForResponse(ex);
        var plan = new CompassPlanResult(
            SchemaVersion: CompassPlannerStructuredOutputContract.SchemaVersion,
            Summary: "The Compass planner crashed while building this plan. Tell the user we hit an internal error and offer to retry.",
            Steps: [],
            Confidence: 0m,
            ReasonCodes: ["sub_agent_exception"],
            Entities: [],
            Warnings: [message]);
        var planJson = JsonSerializer.Serialize(plan, CompassPlannerStructuredOutputContract.SerializerOptions);
        return new CompassPlannerAgentToolResponse(plan, planJson);
    }
}
