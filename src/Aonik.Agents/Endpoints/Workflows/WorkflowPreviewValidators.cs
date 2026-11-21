using FastEndpoints;
using FluentValidation;

using Aonik.SharedKernel.Validation;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed class ValidateWorkflowRequestValidator : Validator<ValidateWorkflowRequest>
{
    public ValidateWorkflowRequestValidator()
    {
        RuleFor(request => request.Slug).RequiredText(100).Matches("^[a-z0-9][a-z0-9_-]*$");
    }
}

internal sealed class SimulateWorkflowRequestValidator : Validator<SimulateWorkflowRequest>
{
    public SimulateWorkflowRequestValidator()
    {
        RuleFor(request => request.Slug).RequiredText(100).Matches("^[a-z0-9][a-z0-9_-]*$");
        RuleFor(request => request.Facts).NotNull();
        RuleFor(request => request.Facts.Count).LessThanOrEqualTo(64).When(request => request.Facts is not null);
        RuleFor(request => request.ActionOutputs!.Count).LessThanOrEqualTo(100)
            .When(request => request.ActionOutputs is not null);
    }
}
