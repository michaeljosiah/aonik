using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Rooms;

internal sealed class ChatRoomAgentsEndpoint(IChatRoomService rooms) : ChatRoomWithoutRequestEndpoint<IReadOnlyList<RoomAgentOption>>
{
    public override void Configure()
    {
        Get("/ai/rooms/agents");
        Policies("AdminReadPolicy");
    }

    public override Task HandleAsync(CancellationToken ct)
        => ExecuteAsync(() => rooms.AgentsAsync(ct), ct);
}
