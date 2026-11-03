using System.Text.Json;
using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Services;

internal sealed class ChatRoomService(
    AgentsDbContext dbContext,
    ITenantProvider tenantProvider,
    ICurrentUserProvider currentUserProvider,
    IChatParticipantDirectory directory,
    IChatRoomAgentRunner agentRunner,
    IAiRunWriter aiRunWriter,
    IToolApprovalService approvalService,
    ChatRoomInvocationGuard invocationGuard,
    IClock clock,
    ILogger<ChatRoomService> logger) : IChatRoomService
{
    private static readonly string[] ContextTypes = ["Invoice", "Order", "PaymentIntent", "LedgerEntry", "Proposal", "Customer"];
    private Guid TenantId => tenantProvider.GetCurrentTenantId();
    private Guid UserId => currentUserProvider.TryGetCurrentUserId(out var id) && id != Guid.Empty ? id : throw new ChatRoomException(401, "Authentication is required.");
    private static string HumanKey(Guid id) => $"human:{id:D}";
    private static string AgentKey(string name) => $"agent:{name}";
    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json)!;

    private bool _canInvokeAgents;
    private async Task<ChatWorkspacePerson> CurrentPersonAsync(CancellationToken ct)
    {
        var person = (await directory.GetByIdsAsync(TenantId, [UserId], ct)).SingleOrDefault()
            ?? throw new ChatRoomException(403, "An active workspace staff account is required.");
        _canInvokeAgents = person.CanRequestAgentActions;
        return person;
    }

    private async Task<(ChatRoom Room, ChatRoomMember Member)> AccessAsync(Guid roomId, bool write, CancellationToken ct)
    {
        await CurrentPersonAsync(ct);
        var room = await dbContext.ChatRooms.SingleOrDefaultAsync(room => room.Id == roomId && room.TenantId == TenantId, ct);
        var member = await dbContext.ChatRoomMembers.SingleOrDefaultAsync(member => member.RoomId == roomId && member.TenantId == TenantId && member.UserId == UserId && member.IsActive, ct);
        if (room is null || member is null) throw new ChatRoomException(404, "Room not found.");
        if (write && member.Role is not ("Owner" or "Participant")) throw new ChatRoomException(403, "Observers can read the room but cannot post or request agent actions.");
        return (room, member);
    }

    public async Task<IReadOnlyList<ChatRoomSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        await CurrentPersonAsync(cancellationToken);
        var ids = dbContext.ChatRoomMembers.Where(member => member.TenantId == TenantId && member.UserId == UserId && member.IsActive).Select(member => member.RoomId);
        var rooms = await dbContext.ChatRooms.AsNoTracking().Where(room => room.TenantId == TenantId && ids.Contains(room.Id))
            .OrderByDescending(room => room.LastActivityAt).Take(100).ToListAsync(cancellationToken);
        var roomIds = rooms.Select(room => room.Id).ToList();
        var members = await dbContext.ChatRoomMembers.AsNoTracking().Where(member => roomIds.Contains(member.RoomId) && member.TenantId == TenantId && member.IsActive).ToListAsync(cancellationToken);
        return rooms.Select(room => Summary(room, members.Where(member => member.RoomId == room.Id).ToList())).ToList();
    }

    public async Task<ChatRoomDetail> CreateAsync(CreateChatRoomRequest request, CancellationToken cancellationToken = default)
    {
        var person = await CurrentPersonAsync(cancellationToken);
        var title = request.Title?.Trim() ?? "";
        if (title.Length is < 1 or > 160) throw new ChatRoomException(400, "Room title must contain 1–160 characters.");
        var context = request.Context ?? [];
        if (context.Count > 12 || context.Any(item => !ContextTypes.Contains(item.Type) || string.IsNullOrWhiteSpace(item.Reference) || item.Reference.Length > 100 || item.Reference.Any(char.IsControl)))
            throw new ChatRoomException(400, "Use up to twelve typed record references, each at most 100 characters.");
        var room = new ChatRoom { TenantId = TenantId, OwnerUserId = UserId, Title = title, ContextJson = JsonSerializer.Serialize(context), LastActivityAt = clock.UtcNow };
        dbContext.ChatRooms.Add(room);
        dbContext.ChatRoomMembers.Add(new ChatRoomMember { TenantId = TenantId, RoomId = room.Id, ActorKey = HumanKey(UserId), UserId = UserId, DisplayName = person.DisplayName, Role = "Owner", LastSeenAt = clock.UtcNow });
        dbContext.ChatRoomMembers.Add(new ChatRoomMember { TenantId = TenantId, RoomId = room.Id, ActorKey = AgentKey("master-orchestrator"), AgentName = "master-orchestrator", DisplayName = "AONIK", Role = "Agent" });
        Event(room, "Created this shared room. Private chat history was not copied.");
        await SaveAsync(cancellationToken);
        return await GetAsync(room.Id, cancellationToken: cancellationToken);
    }

    public async Task<ChatRoomDetail> GetAsync(Guid roomId, long? before = null, CancellationToken cancellationToken = default)
    {
        var (room, _) = await AccessAsync(roomId, false, cancellationToken);
        var members = await dbContext.ChatRoomMembers.AsNoTracking().Where(member => member.RoomId == roomId && member.TenantId == TenantId && member.IsActive).ToListAsync(cancellationToken);
        var messages = await dbContext.ChatRoomMessages.AsNoTracking().Where(message => message.RoomId == roomId && message.TenantId == TenantId && (!before.HasValue || message.Sequence < before.Value))
            .OrderByDescending(message => message.Sequence).Take(100).ToListAsync(cancellationToken);
        var messageIds = messages.Select(message => message.Id).ToList();
        var turnIds = messages.Where(message => message.TurnId.HasValue).Select(message => message.TurnId!.Value).ToList();
        var recentSince = clock.UtcNow.AddMinutes(-20);
        var turns = await dbContext.ChatRoomTurns.AsNoTracking().Where(turn => turn.TenantId == TenantId && turn.RoomId == roomId
            && (turnIds.Contains(turn.Id) || messageIds.Contains(turn.PromptMessageId) || turn.Id == room.ActiveTurnId || turn.StartedAt >= recentSince)).OrderByDescending(turn => turn.StartedAt).Take(300).ToListAsync(cancellationToken);
        var approvalIds = turns.SelectMany(turn => Read<List<Guid>>(turn.ApprovalIdsJson)).Distinct().ToList();
        var approvals = await dbContext.ToolApprovalRequests.AsNoTracking().Where(item => item.TenantId == TenantId && approvalIds.Contains(item.Id)).ToListAsync(cancellationToken);
        var turnDtos = turns.Select(turn => TurnDto(turn, members, approvals)).ToList();
        return new ChatRoomDetail(Summary(room, members), members.Select(member => new ChatRoomParticipant(member.ActorKey, member.UserId, member.AgentName, member.DisplayName, member.Role, member.LastSeenAt)).ToList(),
            Read<List<RoomContextReference>>(room.ContextJson), messages.OrderBy(message => message.Sequence).Select(MessageDto).ToList(), turnDtos, messages.Count > 0 && messages.Min(message => message.Sequence) > 1, UserId);
    }

    public async Task<IReadOnlyList<ChatWorkspacePerson>> PeopleAsync(string? search, CancellationToken cancellationToken = default)
    {
        await CurrentPersonAsync(cancellationToken);
        if (search?.Length > 100) throw new ChatRoomException(400, "Search is too long.");
        return await directory.SearchAsync(TenantId, search, cancellationToken);
    }

    public async Task<IReadOnlyList<RoomAgentOption>> AgentsAsync(CancellationToken cancellationToken = default)
    {
        await CurrentPersonAsync(cancellationToken);
        return await agentRunner.ListAsync(cancellationToken);
    }

    public async Task AddMemberAsync(Guid roomId, AddChatRoomMemberRequest request, CancellationToken cancellationToken = default)
    {
        var (room, caller) = await AccessAsync(roomId, true, cancellationToken);
        if (caller.Role != "Owner") throw new ChatRoomException(403, "Only the room owner can manage participants.");
        if (!request.ShareHistory) throw new ChatRoomException(400, "Confirm that the participant can see this room's existing history.");
        if (string.IsNullOrWhiteSpace(request.ActorKey) || request.ActorKey.Length > 128) throw new ChatRoomException(400, "Invalid participant identity.");
        var members = await dbContext.ChatRoomMembers.Where(member => member.RoomId == roomId && member.TenantId == TenantId).ToListAsync(cancellationToken);
        if (members.Count(member => member.IsActive) >= 32) throw new ChatRoomException(400, "The room supports up to 32 participants.");
        var existing = members.SingleOrDefault(member => member.ActorKey == request.ActorKey);
        if (existing?.Role == "Owner") throw new ChatRoomException(400, "The room owner cannot be changed here.");
        var member = existing ?? new ChatRoomMember { TenantId = TenantId, RoomId = roomId, ActorKey = request.ActorKey };
        if (request.ActorKey.StartsWith("human:", StringComparison.Ordinal) && Guid.TryParse(request.ActorKey[6..], out var userId) && request.ActorKey == HumanKey(userId))
        {
            if (request.Role is not ("Participant" or "Observer")) throw new ChatRoomException(400, "Choose Participant or Observer.");
            var person = (await directory.GetByIdsAsync(TenantId, [userId], cancellationToken)).SingleOrDefault() ?? throw new ChatRoomException(400, "Select an active staff member from this workspace.");
            member.UserId = userId; member.DisplayName = person.DisplayName; member.Role = request.Role;
        }
        else if (request.ActorKey.StartsWith("agent:", StringComparison.Ordinal))
        {
            var name = request.ActorKey[6..];
            var agent = (await agentRunner.ListAsync(cancellationToken)).SingleOrDefault(agent => agent.Name == name)
                ?? throw new ChatRoomException(400, "Select an available registered agent.");
            member.AgentName = name; member.DisplayName = name == "master-orchestrator" ? "AONIK" : name; member.Role = "Agent";
        }
        else throw new ChatRoomException(400, "Invalid participant identity.");
        var wasActive = existing?.IsActive == true;
        member.IsActive = true;
        if (existing is null) dbContext.ChatRoomMembers.Add(member);
        Event(room, $"{(wasActive ? "Updated" : "Added")} {member.DisplayName} as {member.Role}; room history is shared.");
        await SaveAsync(cancellationToken);
    }

    public async Task RemoveMemberAsync(Guid roomId, string actorKey, CancellationToken cancellationToken = default)
    {
        var (room, caller) = await AccessAsync(roomId, false, cancellationToken);
        if (caller.Role != "Owner" && actorKey != caller.ActorKey) throw new ChatRoomException(403, "Only the owner may remove another participant.");
        var member = await dbContext.ChatRoomMembers.SingleOrDefaultAsync(member => member.RoomId == roomId && member.TenantId == TenantId && member.ActorKey == actorKey && member.IsActive, cancellationToken)
            ?? throw new ChatRoomException(404, "Participant not found.");
        if (member.Role == "Owner") throw new ChatRoomException(400, "The owner must remain in this room.");
        member.IsActive = false; member.LastSeenAt = null;
        Event(room, $"Removed {member.DisplayName} from this room.");
        await SaveAsync(cancellationToken);
    }

    public async Task HeartbeatAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        var (_, member) = await AccessAsync(roomId, false, cancellationToken);
        if (!member.LastSeenAt.HasValue || clock.UtcNow - member.LastSeenAt > TimeSpan.FromSeconds(15))
        {
            member.LastSeenAt = clock.UtcNow;
            await SaveAsync(cancellationToken);
        }
    }

    public async Task<ChatRoomMessageDto> SendAsync(Guid roomId, SendRoomMessageRequest request, CancellationToken cancellationToken = default)
    {
        var (room, member) = await AccessAsync(roomId, true, cancellationToken);
        var content = request.Content?.Trim() ?? "";
        if (request.MentionKeys?.Count > 16) throw new ChatRoomException(400, "Use at most sixteen participant mentions.");
        var mentions = (request.MentionKeys ?? []).Distinct().OrderBy(key => key).ToList();
        if (request.ClientRequestId == Guid.Empty || content.Length is < 1 or > 16000 || mentions.Count > 16) throw new ChatRoomException(400, "Provide a message of 1–16,000 characters and a request reference.");
        var existing = await dbContext.ChatRoomMessages.SingleOrDefaultAsync(message => message.RoomId == roomId && message.TenantId == TenantId && message.ClientRequestId == request.ClientRequestId, cancellationToken);
        var mentionJson = JsonSerializer.Serialize(mentions);
        if (existing is not null)
        {
            if (existing.AuthorUserId != UserId || existing.Content != content || existing.MentionKeysJson != mentionJson) throw new ChatRoomException(409, "This request reference belongs to a different message.");
            return MessageDto(existing);
        }
        var activeKeys = await dbContext.ChatRoomMembers.Where(item => item.RoomId == roomId && item.TenantId == TenantId && item.IsActive).Select(item => item.ActorKey).ToListAsync(cancellationToken);
        if (mentions.Any(key => !activeKeys.Contains(key))) throw new ChatRoomException(400, "Mentions must identify current room participants.");
        var message = Append(room, "Human", content, member.DisplayName, UserId);
        message.ClientRequestId = request.ClientRequestId; message.MentionKeysJson = mentionJson;
        await StageMessageNotificationAsync(message, cancellationToken);
        await SaveAsync(cancellationToken);
        return MessageDto(message);
    }

    public async Task<ChatRoomTurnDto> RunAgentAsync(Guid roomId, RunRoomAgentRequest request, CancellationToken cancellationToken = default)
    {
        var (room, member) = await AccessAsync(roomId, true, cancellationToken);
        if (!_canInvokeAgents) throw new ChatRoomException(403, "Your workspace role cannot request agent actions.");
        if (request.ClientRequestId == Guid.Empty) throw new ChatRoomException(400, "A request reference is required.");
        var existing = await dbContext.ChatRoomTurns.SingleOrDefaultAsync(turn => turn.RoomId == roomId && turn.TenantId == TenantId && turn.ClientRequestId == request.ClientRequestId, cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestedByUserId != UserId || existing.AgentName != request.AgentName || existing.PromptMessageId != request.PromptMessageId) throw new ChatRoomException(409, "This request reference belongs to another agent turn.");
            return await TurnDtoAsync(existing, [member], cancellationToken);
        }
        if (room.ActiveTurnId.HasValue) throw new ChatRoomException(409, "An agent is already responding in this room.");
        var prompt = await dbContext.ChatRoomMessages.SingleOrDefaultAsync(message => message.Id == request.PromptMessageId && message.RoomId == roomId && message.TenantId == TenantId && message.ActorKind == "Human" && message.AuthorUserId == UserId, cancellationToken)
            ?? throw new ChatRoomException(404, "The requesting human's room message was not found.");
        if (!await dbContext.ChatRoomMembers.AnyAsync(item => item.RoomId == roomId && item.TenantId == TenantId && item.AgentName == request.AgentName && item.IsActive, cancellationToken)
            || !(await agentRunner.ListAsync(cancellationToken)).Any(agent => agent.Name == request.AgentName)) throw new ChatRoomException(400, "Add an available agent to this room before addressing it.");
        string? continuation = null;
        if (request.ContinueApprovalId is { } approvalId)
        {
            var priorTurns = await dbContext.ChatRoomTurns.Where(turn => turn.RoomId == roomId && turn.TenantId == TenantId && turn.PromptMessageId == prompt.Id && turn.RequestedByUserId == UserId && turn.AgentName == request.AgentName).ToListAsync(cancellationToken);
            if (!priorTurns.Any(turn => Read<List<Guid>>(turn.ApprovalIdsJson).Contains(approvalId))) throw new ChatRoomException(404, "Room approval not found.");
            var approval = await dbContext.ToolApprovalRequests.SingleOrDefaultAsync(item => item.Id == approvalId && item.TenantId == TenantId && item.RequestingUserId == UserId, cancellationToken);
            if (approval is null || approval.RiskTier != "Medium" || approval.Status != ToolApprovalRequestStatus.Approved || approval.ConsumedAt.HasValue || approval.ExpiresAt <= clock.UtcNow) throw new ChatRoomException(409, "This approval cannot be continued.");
            continuation = JsonSerializer.Serialize(new { approvalId, tool = approval.ToolName, approvedArguments = approval.ArgumentsRedactedJson, instruction = "Continue only this exact approved action. Changed arguments require fresh approval." });
        }
        var turn = new ChatRoomTurn { TenantId = TenantId, RoomId = roomId, ClientRequestId = request.ClientRequestId, PromptMessageId = prompt.Id, RequestedByUserId = UserId, AgentName = request.AgentName, StartedAt = clock.UtcNow };
        dbContext.ChatRoomTurns.Add(turn); room.ActiveTurnId = turn.Id; room.Revision++;
        await SaveAsync(cancellationToken); // Claim durably before any model or tool is invoked.
        invocationGuard.RoomId = roomId; invocationGuard.TenantId = TenantId; invocationGuard.UserId = UserId;
        invocationGuard.AgentName = request.AgentName; invocationGuard.TurnId = turn.Id;
        try
        {
            turn.AiRunId = await aiRunWriter.StartRunAsync("collaborative-chat", JsonSerializer.Serialize(new { roomId, turnId = turn.Id, promptMessageId = prompt.Id, agentName = turn.AgentName, promptVersion = ChatRoomAgentRunner.PromptVersion }), cancellationToken);
            invocationGuard.AiRunId = turn.AiRunId;
            await SaveAsync(cancellationToken);
            var history = await dbContext.ChatRoomMessages.AsNoTracking().Where(message => message.RoomId == roomId && message.TenantId == TenantId && message.Sequence <= prompt.Sequence).OrderByDescending(message => message.Sequence).Take(60).ToListAsync(cancellationToken);
            var context = JsonSerializer.Serialize(new { audience = "All current room participants", roomId, linkedReferences = Read<List<RoomContextReference>>(room.ContextJson), conversation = history.OrderBy(message => message.Sequence).Select(message => new { message.ActorKind, message.AuthorUserId, message.AgentName, contextTruncated = message.Id != prompt.Id && message.Content.Length > 3000, content = message.Id != prompt.Id && message.Content.Length > 3000 ? message.Content[..3000] : message.Content }), continuation });
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(3));
            var result = await agentRunner.RunAsync(request.AgentName, context, timeout.Token);
            await PersistOutcomeAsync(room, turn, result);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Shared room turn {TurnId} was interrupted", turn.Id);
            await PersistOutcomeAsync(room, turn, new RoomAgentResult("", Read<List<Guid>>(turn.ApprovalIdsJson),
                "The run was interrupted. Review existing records and approvals before starting another request."));
        }
        if (turn.AiRunId.HasValue)
        {
            try
            {
                if (turn.Status == "Completed") await aiRunWriter.MarkRunCompletedAsync(turn.AiRunId.Value, turn.Id.ToString(), CancellationToken.None);
                else await aiRunWriter.MarkRunFailedAsync(turn.AiRunId.Value, turn.Error ?? "Room turn interrupted", CancellationToken.None);
            }
            catch (Exception exception) { logger.LogWarning(exception, "Could not update audit outcome for room turn {TurnId}", turn.Id); }
        }
        await AccessAsync(roomId, false, cancellationToken);
        return await TurnDtoAsync(turn, [member], cancellationToken);
    }

    private async Task PersistOutcomeAsync(ChatRoom room, ChatRoomTurn turn, RoomAgentResult result)
    {
        // Retry only this database commit. The model and tools are never invoked again.
        for (var attempt = 0; ; attempt++)
        {
            await dbContext.Entry(room).ReloadAsync(CancellationToken.None);
            await dbContext.Entry(turn).ReloadAsync(CancellationToken.None);
            if (turn.CompletedAt.HasValue && turn.Status != "Running") return;
            var allowed = room.ActiveTurnId == turn.Id
                && await dbContext.ChatRoomMembers.AsNoTracking().AnyAsync(member => member.RoomId == room.Id && member.TenantId == TenantId && member.UserId == UserId && member.IsActive && (member.Role == "Owner" || member.Role == "Participant"), CancellationToken.None)
                && await dbContext.ChatRoomMembers.AsNoTracking().AnyAsync(member => member.RoomId == room.Id && member.TenantId == TenantId && member.AgentName == turn.AgentName && member.IsActive, CancellationToken.None)
                && (await directory.GetByIdsAsync(TenantId, [UserId], CancellationToken.None)).SingleOrDefault()?.CanRequestAgentActions == true;
            turn.Status = result.Error is null && allowed ? "Completed" : "Unconfirmed";
            turn.Error = allowed ? result.Error : "Room execution authority changed during this run.";
            turn.ApprovalIdsJson = JsonSerializer.Serialize(result.ApprovalIds.Distinct());
            turn.CompletedAt = clock.UtcNow;
            ChatRoomMessage? reply = null;
            if (allowed && !string.IsNullOrWhiteSpace(result.Content))
            {
                reply = Append(room, "Agent", result.Content.Length > 32000 ? result.Content[..32000] : result.Content,
                    turn.AgentName == "master-orchestrator" ? "AONIK" : turn.AgentName);
                reply.AgentName = turn.AgentName; reply.AiRunId = turn.AiRunId; reply.TurnId = turn.Id;
            }
            if (room.ActiveTurnId == turn.Id) room.ActiveTurnId = null;
            room.Revision++;
            try
            {
                if (reply is not null) await StageMessageNotificationAsync(reply, CancellationToken.None);
                await SaveAsync(CancellationToken.None);
                return;
            }
            catch (ChatRoomException exception) when (exception.StatusCode == 409)
            {
                DetachPendingReply(reply);
                if (attempt >= 4) throw;
            }
            catch
            {
                DetachPendingReply(reply);
                throw;
            }
        }
    }

    private void DetachPendingReply(ChatRoomMessage? reply)
    {
        if (reply is null) return;
        dbContext.Entry(reply).State = EntityState.Detached;
        foreach (var staged in dbContext.ChangeTracker.Entries<OutboxMessage>().Where(entry => entry.State == EntityState.Added && entry.Entity.EventId == reply.Id).ToList())
            staged.State = EntityState.Detached;
    }

    public async Task DecideAsync(Guid roomId, DecideRoomApprovalRequest request, CancellationToken cancellationToken = default)
    {
        var (room, _) = await AccessAsync(roomId, true, cancellationToken);
        if (!_canInvokeAgents) throw new ChatRoomException(403, "Your workspace role cannot decide agent actions.");
        var turn = await dbContext.ChatRoomTurns.SingleOrDefaultAsync(turn => turn.Id == request.TurnId && turn.RoomId == roomId && turn.TenantId == TenantId && turn.RequestedByUserId == UserId, cancellationToken);
        if (turn is null || !Read<List<Guid>>(turn.ApprovalIdsJson).Contains(request.ApprovalId)) throw new ChatRoomException(404, "Room approval not found.");
        if (request.Decision is not ("Approve" or "Reject")) throw new ChatRoomException(400, "Choose Approve or Reject.");
        var result = await approvalService.DecideAsync(request.ApprovalId, new ToolApprovalDecisionInput(request.Decision == "Approve" ? ToolApprovalDecisionType.Approve : ToolApprovalDecisionType.Reject, request.Reason), cancellationToken);
        if (result.Outcome is not (ToolApprovalDecisionOutcome.Approved or ToolApprovalDecisionOutcome.Rejected)) throw new ChatRoomException(result.Outcome == ToolApprovalDecisionOutcome.Forbidden ? 403 : 409, result.Message ?? "The approval could not be decided.");
        Event(room, $"{request.Decision} decision recorded for approval {request.ApprovalId}. Execution status remains owned by the system.");
        await SaveAsync(cancellationToken);
    }

    public async Task CloseInterruptedTurnAsync(Guid roomId, Guid turnId, CancellationToken cancellationToken = default)
    {
        var (room, member) = await AccessAsync(roomId, true, cancellationToken);
        if (member.Role != "Owner") throw new ChatRoomException(403, "Only the owner can close an interrupted run.");
        var turn = await dbContext.ChatRoomTurns.SingleOrDefaultAsync(turn => turn.Id == turnId && turn.RoomId == roomId && turn.TenantId == TenantId, cancellationToken) ?? throw new ChatRoomException(404, "Run not found.");
        if (turn.Status != "Running" || clock.UtcNow - turn.StartedAt < TimeSpan.FromMinutes(10)) throw new ChatRoomException(409, "Only a run interrupted for at least ten minutes may be closed.");
        turn.Status = "Unconfirmed"; turn.CompletedAt = clock.UtcNow; turn.Error = "Closed after interruption. Review approvals and records before a new request.";
        if (room.ActiveTurnId == turn.Id) room.ActiveTurnId = null;
        Event(room, "Closed an interrupted agent run without retrying or claiming execution succeeded.");
        await SaveAsync(cancellationToken);
    }

    private ChatRoomSummary Summary(ChatRoom room, IReadOnlyList<ChatRoomMember> members) => new(room.Id, room.Title, members.FirstOrDefault(member => member.UserId == UserId)?.Role ?? "Observer", room.Revision, room.LastActivityAt, members.Count(member => member.UserId.HasValue), members.Count(member => member.AgentName is not null));
    private static ChatRoomMessageDto MessageDto(ChatRoomMessage message) => new(message.Id, message.Sequence, message.ActorKind, message.AuthorUserId, message.AgentName, message.AuthorName, message.Content, Read<List<string>>(message.MentionKeysJson), message.AiRunId, message.TurnId, message.CreatedAt);

    private async Task<ChatRoomTurnDto> TurnDtoAsync(ChatRoomTurn turn, IReadOnlyList<ChatRoomMember> members, CancellationToken ct)
    {
        var ids = Read<List<Guid>>(turn.ApprovalIdsJson);
        var approvals = await dbContext.ToolApprovalRequests.AsNoTracking().Where(item => ids.Contains(item.Id) && item.TenantId == TenantId).ToListAsync(ct);
        return TurnDto(turn, members, approvals);
    }

    private ChatRoomTurnDto TurnDto(ChatRoomTurn turn, IReadOnlyList<ChatRoomMember> members, IReadOnlyList<ToolApprovalRequest> approvals)
    {
        var ids = Read<List<Guid>>(turn.ApprovalIdsJson);
        var role = members.FirstOrDefault(member => member.UserId == UserId)?.Role;
        return new(turn.Id, turn.PromptMessageId, turn.RequestedByUserId, turn.AgentName, turn.Status, turn.AiRunId, turn.StartedAt, turn.Error,
            approvals.Where(item => ids.Contains(item.Id)).Select(item => new ChatRoomApproval(item.Id, item.ProposalId, item.ToolName, item.RiskTier, item.ActionKind,
                item.Status == ToolApprovalRequestStatus.Pending && item.ExpiresAt <= clock.UtcNow ? "Expired" : item.Status.ToString(),
                item.ArgumentsRedactedJson, _canInvokeAgents && item.RequestingUserId == UserId && (role is "Owner" or "Participant") && item.Status == ToolApprovalRequestStatus.Pending && item.ExpiresAt > clock.UtcNow,
                _canInvokeAgents && item.RequestingUserId == UserId && (role is "Owner" or "Participant") && item.RiskTier == "Medium" && item.Status == ToolApprovalRequestStatus.Approved && !item.ConsumedAt.HasValue && item.ExpiresAt > clock.UtcNow)).ToList(), role == "Owner" && turn.Status == "Running" && clock.UtcNow - turn.StartedAt >= TimeSpan.FromMinutes(10));
    }

    private ChatRoomMessage Append(ChatRoom room, string kind, string content, string author, Guid? userId = null)
    {
        room.LastSequence++; room.Revision++; room.LastActivityAt = clock.UtcNow;
        var message = new ChatRoomMessage { TenantId = TenantId, RoomId = room.Id, Sequence = room.LastSequence, ActorKind = kind, AuthorName = author, AuthorUserId = userId, Content = content };
        dbContext.ChatRoomMessages.Add(message);
        return message;
    }
    private void Event(ChatRoom room, string content) => Append(room, "System", content, "Room activity", UserId);
    private async Task StageMessageNotificationAsync(ChatRoomMessage message, CancellationToken ct)
    {
        var recipients = await dbContext.ChatRoomMembers.AsNoTracking()
            .Where(member => member.TenantId == TenantId && member.RoomId == message.RoomId && member.IsActive
                && member.UserId.HasValue && member.UserId != message.AuthorUserId)
            .Select(member => member.UserId!.Value).ToListAsync(ct);
        if (recipients.Count > 0)
            dbContext.EnqueueIntegrationEvent(new ChatRoomMessagePostedEvent(TenantId, message.RoomId, message.Id, recipients, clock.UtcNow));
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await dbContext.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ChatRoomException(409, "The room changed. Refresh and retry with the same request reference."); }
        catch (DbUpdateException) { throw new ChatRoomException(409, "The request conflicts with a concurrent room update. Refresh before retrying."); }
    }
}
