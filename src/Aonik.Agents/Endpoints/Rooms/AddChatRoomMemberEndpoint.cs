using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Rooms;

internal sealed class AddChatRoomMemberEndpoint(IChatRoomService rooms) : ChatRoomEndpoint<AddChatRoomMemberRequest, RoomActionResponse>
{
    public override void Configure()
    {
        Post("/ai/rooms/{RoomId:guid}/members");
        Policies("AdminReadPolicy");
    }

    public override Task HandleAsync(AddChatRoomMemberRequest req, CancellationToken ct)
        => ExecuteAsync(() => DoneAsync(() => rooms.AddMemberAsync(Route<Guid>("RoomId"), req, ct)), ct);
}
