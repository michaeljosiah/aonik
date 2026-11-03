using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Rooms;

internal sealed class ListChatRoomsEndpoint(IChatRoomService rooms) : ChatRoomWithoutRequestEndpoint<IReadOnlyList<ChatRoomSummary>>
{
    public override void Configure()
    {
        Get("/ai/rooms");
        Policies("AdminReadPolicy");
    }

    public override Task HandleAsync(CancellationToken ct)
        => ExecuteAsync(() => rooms.ListAsync(ct), ct);
}
