using FastEndpoints;
using FluentValidation;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.SharedKernel.Validation;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed class WorkflowGovernanceCommandValidator : Validator<WorkflowGovernanceCommand>
{
    public WorkflowGovernanceCommandValidator()
    {
        RuleFor(request => request.Slug).RequiredText(100).Matches("^[a-z0-9][a-z0-9_-]*$");
        RuleFor(request => request.ExpectedVersion).NotNull().MaximumLength(128);
        RuleFor(request => request.DefinitionHash).NotEmpty().Matches("^[a-f0-9]{64}$");
    }
}
