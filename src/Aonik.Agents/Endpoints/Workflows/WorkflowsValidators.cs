using Aonik.SharedKernel.Validation;
using FastEndpoints;
using FluentValidation;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed class ListWorkflowsRequestValidator : Validator<ListWorkflowsRequest>
{
    public ListWorkflowsRequestValidator()
    {
        RuleFor(request => request.Skip).InclusiveBetween(0, 100_000).When(request => request.Skip.HasValue);
        RuleFor(request => request.Take).InclusiveBetween(1, 100).When(request => request.Take.HasValue);
    }
}

internal sealed class DeleteWorkflowRequestValidator : Validator<DeleteWorkflowRequest>
{
    public DeleteWorkflowRequestValidator()
    {
        RuleFor(x => x.Slug)
            .RequiredText(100)
            .Matches("^[a-z0-9][a-z0-9_-]*$")
            .WithMessage("Slug must be lowercase alphanumerics and hyphens, starting with a letter or digit.");
    }
}

internal sealed class GetWorkflowBySlugRequestValidator : Validator<GetWorkflowBySlugRequest>
{
    public GetWorkflowBySlugRequestValidator()
    {
        RuleFor(x => x.Slug)
            .RequiredText(100)
            .Matches("^[a-z0-9][a-z0-9_-]*$")
            .WithMessage("Slug must be lowercase alphanumerics and hyphens, starting with a letter or digit.");
    }
}

internal sealed class ListWorkflowRunsRequestValidator : Validator<ListWorkflowRunsRequest>
{
    public ListWorkflowRunsRequestValidator()
    {
        RuleFor(x => x.WorkflowId).RequiredId();
        RuleFor(x => x.Take)
            .InclusiveBetween(1, 500).WithMessage("Take must be between 1 and 500.")
            .When(x => x.Take.HasValue);
    }
}

internal sealed class ListWorkflowVersionsRequestValidator : Validator<ListWorkflowVersionsRequest>
{
    public ListWorkflowVersionsRequestValidator()
    {
        RuleFor(x => x.WorkflowId).RequiredId();
    }
}
