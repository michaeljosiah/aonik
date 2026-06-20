using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.SharedKernel.Validation;
using FastEndpoints;
using FluentValidation;

namespace Aonik.Finance.Endpoints.PersonalFinance;

// ════════════════════════════════════════════════════════════════════
// Validators for the AONIK Compass (Spec 021) request DTOs. Required by the
// RequestDtoValidatorCoverageTests architecture rule — every endpoint TRequest
// must have a Validator<T> or an explicit [NoValidation] opt-out. The endpoint
// DTOs are internal, so their validators are internal too (a public validator
// over an internal type is an accessibility error); FastEndpoints discovers
// internal validators just the same.
// ════════════════════════════════════════════════════════════════════

internal sealed class ListGoalsRequestValidator : Validator<ListGoalsRequest>
{
    public ListGoalsRequestValidator()
        => RuleFor(x => x.Status).MaximumLength(40).When(x => x.Status is not null);
}

internal sealed class GetGoalRequestValidator : Validator<GetGoalRequest>
{
    public GetGoalRequestValidator() => RuleFor(x => x.GoalId).RequiredId();
}

internal sealed class UpdateGoalRouteRequestValidator : Validator<UpdateGoalRouteRequest>
{
    public UpdateGoalRouteRequestValidator()
    {
        RuleFor(x => x.GoalId).RequiredId();
        RuleFor(x => x.Name).MaximumLength(256).When(x => x.Name is not null);
        RuleFor(x => x.TargetAmount).GreaterThan(0m).When(x => x.TargetAmount.HasValue);
    }
}

internal sealed class GetGoalPlanRequestValidator : Validator<GetGoalPlanRequest>
{
    public GetGoalPlanRequestValidator() => RuleFor(x => x.GoalId).RequiredId();
}

internal sealed class GetGoalPlanHistoryRequestValidator : Validator<GetGoalPlanHistoryRequest>
{
    public GetGoalPlanHistoryRequestValidator() => RuleFor(x => x.GoalId).RequiredId();
}

internal sealed class GenerateGoalPlanRequestValidator : Validator<GenerateGoalPlanRequest>
{
    public GenerateGoalPlanRequestValidator() => RuleFor(x => x.GoalId).RequiredId();
}

internal sealed class GetGoalGuidanceRequestValidator : Validator<GetGoalGuidanceRequest>
{
    public GetGoalGuidanceRequestValidator() => RuleFor(x => x.GoalId).RequiredId();
}

internal sealed class GetSafeToSpendRequestValidator : Validator<GetSafeToSpendRequest>
{
    // AsOfDate is an optional snapshot date with no further constraint — an empty
    // ruleset is the explicit "nothing to validate" for this endpoint.
    public GetSafeToSpendRequestValidator()
    {
    }
}

internal sealed class CreateGoalRequestValidator : Validator<CreateGoalRequest>
{
    public CreateGoalRequestValidator()
    {
        RuleFor(x => x.Name).RequiredText(256);
        RuleFor(x => x.TargetAmount).GreaterThan(0m);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.ProgressAmount).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.Priority).InclusiveBetween(1, 5).When(x => x.Priority.HasValue);
    }
}

internal sealed class CreateCompassProposalRequestValidator : Validator<CreateCompassProposalRequest>
{
    public CreateCompassProposalRequestValidator()
    {
        RuleFor(x => x.GoalId).RequiredId();
        RuleFor(x => x.ActionType).RequiredText(80);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Rationale).MaximumLength(2000);
    }
}
