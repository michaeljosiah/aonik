using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Agents.Services;

internal sealed class ChatRoomInvocationGuard(IServiceScopeFactory scopeFactory) : IAgentMutationGuard
{
    public Guid? RoomId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string AgentName { get; set; } = "";
    public Guid TurnId { get; set; }
    public Guid? AiRunId { get; set; }

    public async Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        if (!RoomId.HasValue) return; // Existing private transports retain their existing boundary.
        using var scope = scopeFactory.CreateScope();
        ChatThreadManager.SeedBackgroundContext(scope.ServiceProvider, TenantId, UserId, "room-tool-gate");
        var db = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
        var directory = scope.ServiceProvider.GetRequiredService<IChatParticipantDirectory>();
        var humanActive = await db.ChatRoomMembers.AsNoTracking().AnyAsync(member => member.TenantId == TenantId && member.RoomId == RoomId && member.UserId == UserId && member.IsActive && (member.Role == "Owner" || member.Role == "Participant"), cancellationToken);
        var agentActive = await db.ChatRoomMembers.AsNoTracking().AnyAsync(member => member.TenantId == TenantId && member.RoomId == RoomId && member.AgentName == AgentName && member.IsActive, cancellationToken);
        var runActive = await db.ChatRooms.AsNoTracking().AnyAsync(room => room.TenantId == TenantId && room.Id == RoomId && room.ActiveTurnId == TurnId, cancellationToken);
        if (!humanActive || !agentActive || !runActive || (await directory.GetByIdsAsync(TenantId, [UserId], cancellationToken)).SingleOrDefault()?.CanRequestAgentActions != true)
            throw new ChatRoomException(403, "Room execution authority changed. This mutation is refused.");
    }
}
