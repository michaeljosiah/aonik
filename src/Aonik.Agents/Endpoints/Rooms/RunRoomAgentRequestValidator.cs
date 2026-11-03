using FastEndpoints;
using FluentValidation;

using Aonik.Agents.Contracts.Models;

namespace Aonik.Agents.Endpoints.Rooms;

internal sealed class RunRoomAgentRequestValidator : Validator<RunRoomAgentRequest>
{
    public RunRoomAgentRequestValidator()
    {
        RuleFor(request => request.ClientRequestId).NotEmpty();
        RuleFor(request => request.PromptMessageId).NotEmpty();
        RuleFor(request => request.AgentName).NotEmpty().MaximumLength(128);
        RuleFor(request => request.ContinueApprovalId).Must(id => !id.HasValue || id.Value != Guid.Empty).WithMessage("Provide a valid approval reference.");
    }
}
