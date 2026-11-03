using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Rooms;

internal sealed class CloseChatRoomTurnEndpoint(IChatRoomService rooms) : ChatRoomWithoutRequestEndpoint<RoomActionResponse>
{
    public override void Configure()
    {
        Post("/ai/rooms/{RoomId:guid}/turns/{TurnId:guid}/close");
        Policies("AdminWritePolicy");
    }

    public override Task HandleAsync(CancellationToken ct)
        => ExecuteAsync(() => DoneAsync(() => rooms.CloseInterruptedTurnAsync(Route<Guid>("RoomId"), Route<Guid>("TurnId"), ct)), ct);
}
