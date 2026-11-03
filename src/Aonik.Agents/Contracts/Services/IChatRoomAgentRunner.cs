using Aonik.Agents.Contracts.Models;

namespace Aonik.Agents.Contracts.Services;

public sealed record RoomAgentResult(string Content, IReadOnlyList<Guid> ApprovalIds, string? Error = null);
public interface IChatRoomAgentRunner
{
    Task<IReadOnlyList<RoomAgentOption>> ListAsync(CancellationToken cancellationToken = default);
    Task<RoomAgentResult> RunAsync(string agentName, string roomContext, CancellationToken cancellationToken = default);
}
