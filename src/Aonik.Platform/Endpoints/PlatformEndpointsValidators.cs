using Aonik.Platform.Endpoints.Admin.Alerts;
using Aonik.Platform.Endpoints.Admin.Compliance;
using Aonik.Platform.Endpoints.Admin.Customers;
using Aonik.Platform.Endpoints.Admin.Jobs;
using Aonik.Platform.Endpoints.Admin.Notifications;
using Aonik.Platform.Endpoints.Admin.Observability;
using Aonik.Platform.Endpoints.Cms;
using Aonik.SharedKernel.Validation;
using FastEndpoints;
using FluentValidation;

namespace Aonik.Platform.Endpoints;

// ────────────────────────────────────────────────────────────────────
// Validators for internal-visibility endpoint-level request DTOs that
// live next to their endpoint files.
// ────────────────────────────────────────────────────────────────────

internal sealed class ListAlertsRequestValidator : Validator<ListAlertsRequest>
{
    public ListAlertsRequestValidator() => RuleFor(x => x.Take).InclusiveBetween(1, 500);
}

internal sealed class ListAuditLogsEndpointRequestValidator : Validator<ListAuditLogsEndpointRequest>
{
    public ListAuditLogsEndpointRequestValidator()
    {
        RuleFor(x => x.PageNumber).PageNumber();
        RuleFor(x => x.PageSize).PageSize(1, 200);
        RuleFor(x => x.Search).MaximumLength(256);
        RuleFor(x => x.Action).MaximumLength(128);
    }
}

internal sealed class ImportCustomerDataRequestValidator : Validator<ImportCustomerDataRequest>
{
    public ImportCustomerDataRequestValidator()
    {
        RuleFor(x => x.Bundle).NotNull().WithMessage("Bundle is required.");
        RuleFor(x => x.ConflictMode)
            .NotEmpty()
            .Must(m => m is "fail" or "skip")
            .WithMessage("ConflictMode must be 'fail' or 'skip'.");
    }
}

internal sealed class ListScheduledJobCommandsRequestValidator : Validator<ListScheduledJobCommandsRequest>
{
    public ListScheduledJobCommandsRequestValidator()
    {
        RuleFor(x => x.JobName).RequiredText(128);
        RuleFor(x => x.PageNumber).PageNumber();
        RuleFor(x => x.PageSize).PageSize(1, 200);
    }
}

internal sealed class ListScheduledJobRunsRequestValidator : Validator<ListScheduledJobRunsRequest>
{
    public ListScheduledJobRunsRequestValidator()
    {
        RuleFor(x => x.JobName).RequiredText(128);
        RuleFor(x => x.PageNumber).PageNumber();
        RuleFor(x => x.PageSize).PageSize(1, 200);
    }
}

internal sealed class ListNotificationsRequestValidator : Validator<ListNotificationsRequest>
{
    public ListNotificationsRequestValidator()
    {
        RuleFor(x => x.Status).MaximumLength(64);
        RuleFor(x => x.Take).InclusiveBetween(1, 500);
    }
}

internal sealed class GetObservabilityErrorDetailRequestValidator : Validator<GetObservabilityErrorDetailRequest>
{
    public GetObservabilityErrorDetailRequestValidator()
    {
        RuleFor(x => x.ProblemId).RequiredText(256);
        RuleFor(x => x.TimeRange).RequiredText(32);
    }
}

internal sealed class GenerateContentImageRequestValidator : Validator<GenerateContentImageRequest>
{
    public GenerateContentImageRequestValidator()
    {
        RuleFor(x => x.Prompt)
            .NotEmpty().WithMessage("Prompt is required.")
            .MaximumLength(4_000);
        RuleFor(x => x.Alt).MaximumLength(512);
        RuleFor(x => x.Width)
            .InclusiveBetween(64, 4096)
            .When(x => x.Width.HasValue);
        RuleFor(x => x.Height)
            .InclusiveBetween(64, 4096)
            .When(x => x.Height.HasValue);
    }
}
