using FastEndpoints;
using FluentValidation;

using Aonik.Agents.Contracts.Models;

namespace Aonik.Agents.Endpoints.Rooms;

internal sealed class DecideRoomApprovalRequestValidator : Validator<DecideRoomApprovalRequest>
{
    public DecideRoomApprovalRequestValidator()
    {
        RuleFor(request => request.TurnId).NotEmpty();
        RuleFor(request => request.ApprovalId).NotEmpty();
        RuleFor(request => request.Decision).Must(value => value is "Approve" or "Reject").WithMessage("Choose Approve or Reject.");
        RuleFor(request => request.Reason).MaximumLength(2000);
    }
}
