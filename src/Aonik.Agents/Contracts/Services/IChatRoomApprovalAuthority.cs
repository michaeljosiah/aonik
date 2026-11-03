namespace Aonik.Agents.Contracts.Services;

/// <summary>Preserves room-origin authority on every approval transport, including the global queue.</summary>
public interface IChatRoomApprovalAuthority
{
    Task<bool> CanDecideAsync(Guid? aiRunId, CancellationToken cancellationToken = default);
}
