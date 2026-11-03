using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Rooms;

internal sealed class DecideChatRoomApprovalEndpoint(IChatRoomService rooms) : ChatRoomEndpoint<DecideRoomApprovalRequest, RoomActionResponse>
{
    public override void Configure()
    {
        Post("/ai/rooms/{RoomId:guid}/approvals/decide");
        Policies("AdminWritePolicy");
    }

    public override Task HandleAsync(DecideRoomApprovalRequest req, CancellationToken ct)
        => ExecuteAsync(() => DoneAsync(() => rooms.DecideAsync(Route<Guid>("RoomId"), req, ct)), ct);
}
