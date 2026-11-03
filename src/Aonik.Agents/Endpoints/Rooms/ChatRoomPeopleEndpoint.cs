using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Platform;

namespace Aonik.Agents.Endpoints.Rooms;

internal sealed class ChatRoomPeopleEndpoint(IChatRoomService rooms) : ChatRoomWithoutRequestEndpoint<IReadOnlyList<ChatWorkspacePerson>>
{
    public override void Configure()
    {
        Get("/ai/rooms/people");
        Policies("AdminReadPolicy");
    }

    public override Task HandleAsync(CancellationToken ct)
        => ExecuteAsync(() => rooms.PeopleAsync(Query<string?>("search", isRequired: false), ct), ct);
}
