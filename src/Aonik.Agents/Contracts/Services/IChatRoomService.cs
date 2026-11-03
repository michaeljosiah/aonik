using Aonik.Agents.Contracts.Models;
using Aonik.SharedKernel.Abstractions.Platform;

namespace Aonik.Agents.Contracts.Services;

public interface IChatRoomService
{
    Task<IReadOnlyList<ChatRoomSummary>> ListAsync(CancellationToken cancellationToken = default);
    Task<ChatRoomDetail> CreateAsync(CreateChatRoomRequest request, CancellationToken cancellationToken = default);
    Task<ChatRoomDetail> GetAsync(Guid roomId, long? before = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatWorkspacePerson>> PeopleAsync(string? search, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoomAgentOption>> AgentsAsync(CancellationToken cancellationToken = default);
    Task AddMemberAsync(Guid roomId, AddChatRoomMemberRequest request, CancellationToken cancellationToken = default);
    Task RemoveMemberAsync(Guid roomId, string actorKey, CancellationToken cancellationToken = default);
    Task HeartbeatAsync(Guid roomId, CancellationToken cancellationToken = default);
    Task<ChatRoomMessageDto> SendAsync(Guid roomId, SendRoomMessageRequest request, CancellationToken cancellationToken = default);
    Task<ChatRoomTurnDto> RunAgentAsync(Guid roomId, RunRoomAgentRequest request, CancellationToken cancellationToken = default);
    Task DecideAsync(Guid roomId, DecideRoomApprovalRequest request, CancellationToken cancellationToken = default);
    Task CloseInterruptedTurnAsync(Guid roomId, Guid turnId, CancellationToken cancellationToken = default);
}
