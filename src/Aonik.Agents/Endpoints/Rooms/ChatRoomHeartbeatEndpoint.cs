using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Rooms;

internal sealed class ChatRoomHeartbeatEndpoint(IChatRoomService rooms) : ChatRoomWithoutRequestEndpoint<RoomActionResponse>
{
    public override void Configure()
    {
        Post("/ai/rooms/{RoomId:guid}/presence");
        Policies("AdminReadPolicy");
    }

    public override Task HandleAsync(CancellationToken ct)
        => ExecuteAsync(() => DoneAsync(() => rooms.HeartbeatAsync(Route<Guid>("RoomId"), ct)), ct);
}
