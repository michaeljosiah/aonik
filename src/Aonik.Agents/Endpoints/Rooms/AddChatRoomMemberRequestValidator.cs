using FastEndpoints;
using FluentValidation;

using Aonik.Agents.Contracts.Models;

namespace Aonik.Agents.Endpoints.Rooms;

internal sealed class AddChatRoomMemberRequestValidator : Validator<AddChatRoomMemberRequest>
{
    public AddChatRoomMemberRequestValidator()
    {
        RuleFor(request => request.ActorKey).NotEmpty().MaximumLength(128);
        RuleFor(request => request.Role).Must(role => role is "Participant" or "Observer" or "Agent").WithMessage("Choose Participant, Observer or Agent.");
        RuleFor(request => request.ShareHistory).Equal(true).WithMessage("Confirm that the participant can read the room's existing history.");
    }
}
