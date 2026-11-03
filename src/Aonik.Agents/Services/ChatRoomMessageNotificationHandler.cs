using System.Text.Json;

using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Agents.Services;

internal sealed class ChatRoomMessageNotificationHandler(
    AgentsDbContext dbContext,
    ITenantProvider tenantProvider,
    IChatParticipantDirectory directory,
    IUserNotificationWriter notificationWriter) : IEventHandler<ChatRoomMessagePostedEvent>
{
    public async Task HandleAsync(ChatRoomMessagePostedEvent @event, CancellationToken cancellationToken = default)
    {
        if (tenantProvider.GetCurrentTenantId() != @event.TenantId)
            throw new InvalidOperationException("The room notification tenant must match the dispatch scope.");

        var room = await dbContext.ChatRooms.AsNoTracking().SingleOrDefaultAsync(
            room => room.TenantId == @event.TenantId && room.Id == @event.RoomId, cancellationToken);
        var message = await dbContext.ChatRoomMessages.AsNoTracking().SingleOrDefaultAsync(
            message => message.TenantId == @event.TenantId && message.RoomId == @event.RoomId && message.Id == @event.MessageId, cancellationToken);
        if (room is null || message is null || message.ActorKind is not ("Human" or "Agent")) return;

        var originalRecipients = @event.RecipientUserIds.Distinct().ToList();
        var mentions = JsonSerializer.Deserialize<List<string>>(message.MentionKeysJson) ?? [];
        // Check current membership AND the original audience. Late invitations do not receive old alerts.
        var candidates = await dbContext.ChatRoomMembers.AsNoTracking()
            .Where(member => member.TenantId == @event.TenantId && member.RoomId == room.Id && member.IsActive
                && member.UserId.HasValue && originalRecipients.Contains(member.UserId.Value)
                && member.UserId != message.AuthorUserId && (member.Role == "Owner" || member.Role == "Participant" || member.Role == "Observer"))
            .Select(member => member.UserId!.Value).ToListAsync(cancellationToken);
        var recipients = await directory.GetByIdsAsync(@event.TenantId, candidates, cancellationToken);
        foreach (var recipient in recipients)
        {
            if (!await dbContext.ChatRoomMembers.AsNoTracking().AnyAsync(member => member.TenantId == @event.TenantId
                && member.RoomId == room.Id && member.UserId == recipient.UserId && member.IsActive, cancellationToken)) continue;
            var mentioned = mentions.Contains($"human:{recipient.UserId:D}");
            var agentName = message.ActorKind == "Agent" ? message.AgentName : null;
            var authorName = agentName == "master-orchestrator" ? "AONIK" : message.AuthorName;
            var metadata = JsonSerializer.Serialize(new
            {
                roomId = room.Id, roomTitle = room.Title, messageId = message.Id, sequence = message.Sequence,
                actorKind = message.ActorKind, authorUserId = message.AuthorUserId, authorName, agentName,
                notificationKind = mentioned ? "mention" : "message",
            });
            await notificationWriter.WriteForUserAsync(new UserNotificationWriteRequest(
                @event.TenantId, recipient.UserId, mentioned ? "chat.mention" : "chat.message",
                agentName is null ? "Chat" : "Agent",
                mentioned ? "You were mentioned in a shared room" : "New message in a shared room",
                "Open the room to read the message. Access follows current room membership.", "Info",
                $"/ai/chat?view=rooms&room={room.Id:D}&message={message.Id:D}&sequence={message.Sequence}",
                $"chat:{room.Id:D}:{message.Id:D}", message.AiRunId, metadata,
                IdempotencyKey: $"chat-message-{message.Id:D}", SendPush: false), cancellationToken);
        }
    }
}
