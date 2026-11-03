using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Rooms;

internal sealed class GetChatRoomEndpoint(IChatRoomService rooms) : ChatRoomWithoutRequestEndpoint<ChatRoomDetail>
{
    public override void Configure()
    {
        Get("/ai/rooms/{RoomId:guid}");
        Policies("AdminReadPolicy");
    }

    public override Task HandleAsync(CancellationToken ct)
        => ExecuteAsync(() => rooms.GetAsync(Route<Guid>("RoomId"), Query<long?>("before", isRequired: false), ct), ct);
}
