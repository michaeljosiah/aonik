using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.PersonalFinance.Endpoints;

// ════════════════════════════════════════════════════════════════════
// AONIK Compass endpoints (Spec 021 §8) — the goal → plan → guidance →
// proposal loop, exposed under /personal-finance/compass/* so Payabo can
// back a dedicated Compass surface rather than overloading the bill-pay
// dashboard contract. All UserPolicy-scoped; current user resolved server-side.
// ════════════════════════════════════════════════════════════════════

// ── List goals ──────────────────────────────────────────────────────

internal sealed class ListGoalsRequest
{
    public string? Status { get; set; }
}

internal sealed class ListGoalsEndpoint : Endpoint<ListGoalsRequest, IReadOnlyList<GoalResponse>>
{
    private readonly IGoalService _goalService;

    public ListGoalsEndpoint(IGoalService goalService) => _goalService = goalService;

    public override void Configure()
    {
        Get("/personal-finance/compass/goals");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "List Compass goals";
            s.Description = "Returns the user's goals with Compass programme metadata, optionally filtered by status.";
            s.Response(200, "Goals returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(ListGoalsRequest req, CancellationToken ct)
    {
        var response = await _goalService.ListGoalsAsync(req.Status, ct);
        await Send.OkAsync(response, ct);
    }
}

// ── Get goal ────────────────────────────────────────────────────────

internal sealed class GetGoalRequest
{
    public Guid GoalId { get; set; }
}

internal sealed class GetGoalEndpoint : Endpoint<GetGoalRequest, GoalResponse>
{
    private readonly IGoalService _goalService;

    public GetGoalEndpoint(IGoalService goalService) => _goalService = goalService;

    public override void Configure()
    {
        Get("/personal-finance/compass/goals/{GoalId}");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get a Compass goal";
            s.Description = "Returns a single goal with Compass programme metadata and progress.";
            s.Response(200, "Goal returned successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Goal not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(GetGoalRequest req, CancellationToken ct)
    {
        var response = await _goalService.GetGoalAsync(req.GoalId, ct);
        if (response is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}

// ── Create goal ─────────────────────────────────────────────────────

internal sealed class CreateGoalEndpoint : Endpoint<CreateGoalRequest, GoalResponse>
{
    private readonly IGoalService _goalService;

    public CreateGoalEndpoint(IGoalService goalService) => _goalService = goalService;

    public override void Configure()
    {
        Post("/personal-finance/compass/goals");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Create a Compass goal";
            s.Description = "Creates a goal plus optional Compass programme metadata (goal type, strategy, risk appetite, priority).";
            s.Response(201, "Goal created successfully");
            s.Response(401, "Not authenticated");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CreateGoalRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _goalService.CreateGoalAsync(req, ct);
            await Send.CreatedAtAsync<GetGoalEndpoint>(
                routeValues: new { GoalId = response.GoalId },
                responseBody: response,
                cancellation: ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
    }
}

// ── Update goal ─────────────────────────────────────────────────────

internal sealed class UpdateGoalRouteRequest
{
    public Guid GoalId { get; set; }
    public string? Name { get; set; }
    public decimal? TargetAmount { get; set; }
    public string? Currency { get; set; }
    public DateTime? TargetDate { get; set; }
    public decimal? ProgressAmount { get; set; }
    public Guid? FundingAccountId { get; set; }
    public string? Status { get; set; }
    public string? GoalType { get; set; }
    public string? Strategy { get; set; }
    public string? RiskAppetite { get; set; }
    public int? Priority { get; set; }
    public string? MilestonesJson { get; set; }
}

internal sealed class UpdateGoalEndpoint : Endpoint<UpdateGoalRouteRequest, GoalResponse>
{
    private readonly IGoalService _goalService;

    public UpdateGoalEndpoint(IGoalService goalService) => _goalService = goalService;

    public override void Configure()
    {
        Put("/personal-finance/compass/goals/{GoalId}");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Update a Compass goal";
            s.Description = "Updates a goal and/or its Compass programme metadata. Only provided fields change.";
            s.Response(200, "Goal updated successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Goal not found");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(UpdateGoalRouteRequest req, CancellationToken ct)
    {
        var update = new UpdateGoalRequest(
            Name: req.Name,
            TargetAmount: req.TargetAmount,
            Currency: req.Currency,
            TargetDate: req.TargetDate,
            ProgressAmount: req.ProgressAmount,
            FundingAccountId: req.FundingAccountId,
            Status: req.Status,
            GoalType: req.GoalType,
            Strategy: req.Strategy,
            RiskAppetite: req.RiskAppetite,
            Priority: req.Priority,
            MilestonesJson: req.MilestonesJson);

        try
        {
            var response = await _goalService.UpdateGoalAsync(req.GoalId, update, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
        catch (InvalidOperationException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}

// ── Get current plan ────────────────────────────────────────────────

internal sealed class GetGoalPlanRequest
{
    public Guid GoalId { get; set; }
}

internal sealed class GetGoalPlanEndpoint : Endpoint<GetGoalPlanRequest, CompassPlanResponse>
{
    private readonly ICompassPlanService _planService;

    public GetGoalPlanEndpoint(ICompassPlanService planService) => _planService = planService;

    public override void Configure()
    {
        Get("/personal-finance/compass/goals/{GoalId}/plan");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get a goal's current Compass plan";
            s.Description = "Returns the active Compass plan for the goal, or 404 if none has been generated yet.";
            s.Response(200, "Plan returned successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "No active plan");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(GetGoalPlanRequest req, CancellationToken ct)
    {
        var response = await _planService.GetCurrentPlanAsync(req.GoalId, ct);
        if (response is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}

// ── Get plan history ────────────────────────────────────────────────

internal sealed class GetGoalPlanHistoryRequest
{
    public Guid GoalId { get; set; }
}

internal sealed class GetGoalPlanHistoryEndpoint : Endpoint<GetGoalPlanHistoryRequest, IReadOnlyList<CompassPlanResponse>>
{
    private readonly ICompassPlanService _planService;

    public GetGoalPlanHistoryEndpoint(ICompassPlanService planService) => _planService = planService;

    public override void Configure()
    {
        Get("/personal-finance/compass/goals/{GoalId}/plan/history");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get a goal's Compass plan history";
            s.Description = "Returns all Compass plan versions for the goal, newest first.";
            s.Response(200, "Plan history returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(GetGoalPlanHistoryRequest req, CancellationToken ct)
    {
        var response = await _planService.GetPlanHistoryAsync(req.GoalId, ct);
        await Send.OkAsync(response, ct);
    }
}

// ── Generate plan ───────────────────────────────────────────────────

internal sealed class GenerateGoalPlanRequest
{
    public Guid GoalId { get; set; }
}

internal sealed class GenerateGoalPlanEndpoint : Endpoint<GenerateGoalPlanRequest, CompassPlanResponse>
{
    private readonly ICompassPlanService _planService;

    public GenerateGoalPlanEndpoint(ICompassPlanService planService) => _planService = planService;

    public override void Configure()
    {
        Post("/personal-finance/compass/goals/{GoalId}/plan");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Generate a Compass plan for a goal";
            s.Description = "Generates and persists a new versioned Compass plan, superseding any current plan and recording an AiRun.";
            s.Response(200, "Plan generated successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Goal not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(GenerateGoalPlanRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _planService.GeneratePlanAsync(req.GoalId, ct);
            await Send.OkAsync(response, ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 404);
        }
    }
}

// ── Safe-to-spend ───────────────────────────────────────────────────

internal sealed class GetSafeToSpendRequest
{
    public DateTime? AsOfDate { get; set; }
}

internal sealed class GetSafeToSpendEndpoint : Endpoint<GetSafeToSpendRequest, SafeToSpendResponse>
{
    private readonly ICompassGuidanceService _guidanceService;

    public GetSafeToSpendEndpoint(ICompassGuidanceService guidanceService) => _guidanceService = guidanceService;

    public override void Configure()
    {
        Get("/personal-finance/compass/safe-to-spend");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get Compass safe-to-spend";
            s.Description = "Returns the deterministic safe-to-spend figure (liquid assets minus protected obligations and plan commitments). Marked partial with warnings for mixed-currency or insufficient-data users.";
            s.Response(200, "Safe-to-spend returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(GetSafeToSpendRequest req, CancellationToken ct)
    {
        var response = await _guidanceService.GetSafeToSpendAsync(req.AsOfDate ?? DateTime.UtcNow, ct);
        await Send.OkAsync(response, ct);
    }
}

// ── Goal guidance (goal + plan + safe-to-spend) ─────────────────────

internal sealed class GetGoalGuidanceRequest
{
    public Guid GoalId { get; set; }
}

internal sealed class GetGoalGuidanceEndpoint : Endpoint<GetGoalGuidanceRequest, GoalGuidanceResponse>
{
    private readonly ICompassGuidanceService _guidanceService;

    public GetGoalGuidanceEndpoint(ICompassGuidanceService guidanceService) => _guidanceService = guidanceService;

    public override void Configure()
    {
        Get("/personal-finance/compass/goals/{GoalId}/guidance");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get Compass guidance for a goal";
            s.Description = "Returns the goal, its current plan summary, and the user's current safe-to-spend with warnings.";
            s.Response(200, "Guidance returned successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Goal not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(GetGoalGuidanceRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _guidanceService.GetGoalGuidanceAsync(req.GoalId, ct);
            await Send.OkAsync(response, ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 404);
        }
    }
}

// ── Create Compass proposal ─────────────────────────────────────────

internal sealed class CreateCompassProposalEndpoint : Endpoint<CreateCompassProposalRequest, CompassProposalResponse>
{
    private readonly ICompassGuidanceService _guidanceService;

    public CreateCompassProposalEndpoint(ICompassGuidanceService guidanceService) => _guidanceService = guidanceService;

    public override void Configure()
    {
        Post("/personal-finance/compass/proposals");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Create a Compass recommendation";
            s.Description = "Records a Compass recommendation as a reviewable Proposal (Compass never moves money). Carries goal/plan linkage in its payload.";
            s.Response(201, "Proposal created successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Goal not found");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CreateCompassProposalRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _guidanceService.CreateCompassProposalAsync(req, ct);
            await Send.CreatedAtAsync<ListCompassProposalsEndpoint>(
                routeValues: null,
                responseBody: response,
                cancellation: ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 404);
        }
    }
}

// ── List pending Compass proposals ──────────────────────────────────

internal sealed class ListCompassProposalsEndpoint : EndpointWithoutRequest<IReadOnlyList<CompassProposalResponse>>
{
    private readonly ICompassGuidanceService _guidanceService;

    public ListCompassProposalsEndpoint(ICompassGuidanceService guidanceService) => _guidanceService = guidanceService;

    public override void Configure()
    {
        Get("/personal-finance/compass/proposals");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "List pending Compass recommendations";
            s.Description = "Returns the current user's pending Compass proposals, scoped by proposal type and payload linkage.";
            s.Response(200, "Proposals returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _guidanceService.ListCompassProposalsAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
