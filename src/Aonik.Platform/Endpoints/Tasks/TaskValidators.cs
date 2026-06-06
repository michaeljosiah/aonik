using Aonik.SharedKernel.Abstractions.Tasks;
using FastEndpoints;
using FluentValidation;

namespace Aonik.Platform.Endpoints.Tasks;

/// <summary>
/// Boundary validation for <see cref="ScheduleTaskRequest"/> (Spec 034). Field-level
/// rules reject malformed input with 400; cross-field rules (one-off vs recurring),
/// cron validity, and unknown-ActionType rejection are enforced in <c>WorkItemService</c>
/// and surface as 400 via the endpoint's ArgumentException handling.
/// </summary>
public sealed class ScheduleTaskRequestValidator : Validator<ScheduleTaskRequest>
{
    public ScheduleTaskRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Kind).NotEmpty().MaximumLength(40);
        RuleFor(x => x.ActionType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ActionPayloadJson).NotNull();
        RuleFor(x => x.AssigneeType).NotEmpty().MaximumLength(40);
        RuleFor(x => x.AssigneeKey).MaximumLength(200);
        RuleFor(x => x.SubjectType).MaximumLength(100);
        RuleFor(x => x.RecurrenceCron).MaximumLength(120);
        RuleFor(x => x.Timezone).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.CorrelationId).MaximumLength(200);
        RuleFor(x => x.SourceModule).MaximumLength(100);
        RuleFor(x => x.MaxRuns).GreaterThan(0).When(x => x.MaxRuns.HasValue);
    }
}

/// <summary>Boundary validation for the admin task-list query (Spec 034).</summary>
internal sealed class ListTasksRequestValidator : Validator<ListTasksRequest>
{
    public ListTasksRequestValidator()
    {
        RuleFor(x => x.Status).MaximumLength(40);
        RuleFor(x => x.Take).InclusiveBetween(1, 500);
    }
}
