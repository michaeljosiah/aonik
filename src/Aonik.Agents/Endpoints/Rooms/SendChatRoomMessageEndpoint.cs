using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Rooms;

internal sealed class SendChatRoomMessageEndpoint(IChatRoomService rooms) : ChatRoomEndpoint<SendRoomMessageRequest, ChatRoomMessageDto>
{
    public override void Configure()
    {
        Post("/ai/rooms/{RoomId:guid}/messages");
        Policies("AdminReadPolicy");
    }

    public override Task HandleAsync(SendRoomMessageRequest req, CancellationToken ct)
        => ExecuteAsync(() => rooms.SendAsync(Route<Guid>("RoomId"), req, ct), ct);
}
