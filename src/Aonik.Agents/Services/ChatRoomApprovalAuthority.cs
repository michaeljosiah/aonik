using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Agents.Services;

internal sealed class ChatRoomApprovalAuthority(AgentsDbContext dbContext, ICurrentUserProvider currentUserProvider, IChatParticipantDirectory directory) : IChatRoomApprovalAuthority
{
    public async Task<bool> CanDecideAsync(Guid? aiRunId, CancellationToken cancellationToken = default)
    {
        if (!aiRunId.HasValue || aiRunId == Guid.Empty) return true;
        // Retain the origin check even if a room turn is later soft-deleted. Tenant filtering remains.
        var turn = await dbContext.ChatRoomTurns.IncludeSoftDeleted().AsNoTracking().SingleOrDefaultAsync(turn => turn.AiRunId == aiRunId, cancellationToken);
        if (turn is null) return true; // Existing non-room proposal policy is unchanged.
        if (!currentUserProvider.TryGetCurrentUserId(out var userId) || turn.RequestedByUserId != userId) return false;
        if (!await dbContext.ChatRooms.AnyAsync(room => room.Id == turn.RoomId && room.TenantId == turn.TenantId, cancellationToken)) return false;
        var humanActive = await dbContext.ChatRoomMembers.AnyAsync(member => member.RoomId == turn.RoomId && member.TenantId == turn.TenantId && member.UserId == userId && member.IsActive && (member.Role == "Owner" || member.Role == "Participant"), cancellationToken);
        var agentActive = await dbContext.ChatRoomMembers.AnyAsync(member => member.RoomId == turn.RoomId && member.TenantId == turn.TenantId && member.AgentName == turn.AgentName && member.IsActive, cancellationToken);
        return humanActive && agentActive && (await directory.GetByIdsAsync(turn.TenantId, [userId], cancellationToken)).SingleOrDefault()?.CanRequestAgentActions == true;
    }
}
