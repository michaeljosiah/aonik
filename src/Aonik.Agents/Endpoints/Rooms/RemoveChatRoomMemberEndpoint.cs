using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Rooms;

internal sealed class RemoveChatRoomMemberEndpoint(IChatRoomService rooms) : ChatRoomWithoutRequestEndpoint<RoomActionResponse>
{
    public override void Configure()
    {
        Delete("/ai/rooms/{RoomId:guid}/members/{ActorKey}");
        Policies("AdminReadPolicy");
    }

    public override Task HandleAsync(CancellationToken ct)
        => ExecuteAsync(() => DoneAsync(() => rooms.RemoveMemberAsync(Route<Guid>("RoomId"), Route<string>("ActorKey")!, ct)), ct);
}
