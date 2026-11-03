using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Rooms;

internal sealed class RunChatRoomAgentEndpoint(IChatRoomService rooms) : ChatRoomEndpoint<RunRoomAgentRequest, ChatRoomTurnDto>
{
    public override void Configure()
    {
        Post("/ai/rooms/{RoomId:guid}/turns");
        Policies("AdminWritePolicy");
    }

    public override Task HandleAsync(RunRoomAgentRequest req, CancellationToken ct)
        => ExecuteAsync(() => rooms.RunAgentAsync(Route<Guid>("RoomId"), req, ct), ct);
}
