using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Rooms;

internal sealed class CreateChatRoomEndpoint(IChatRoomService rooms) : ChatRoomEndpoint<CreateChatRoomRequest, ChatRoomDetail>
{
    public override void Configure()
    {
        Post("/ai/rooms");
        Policies("AdminReadPolicy");
    }

    public override Task HandleAsync(CreateChatRoomRequest req, CancellationToken ct)
        => ExecuteAsync(() => rooms.CreateAsync(req, ct), ct);
}
