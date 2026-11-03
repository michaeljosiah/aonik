namespace Aonik.Agents.Contracts.Models;

public sealed record RoomContextReference(string Type, string Reference);
public sealed record ChatRoomSummary(Guid Id, string Title, string Role, long Revision, DateTime LastActivityAt, int HumanCount, int AgentCount);
public sealed record ChatRoomParticipant(string ActorKey, Guid? UserId, string? AgentName, string DisplayName, string Role, DateTime? LastSeenAt);
public sealed record ChatRoomApproval(Guid Id, Guid? ProposalId, string Tool, string Tier, string ActionKind, string Status, string ArgumentsJson, bool CanDecide, bool CanContinue);
public sealed record ChatRoomMessageDto(Guid Id, long Sequence, string ActorKind, Guid? AuthorUserId, string? AgentName, string AuthorName, string Content, IReadOnlyList<string> MentionKeys, Guid? AiRunId, Guid? TurnId, DateTime CreatedAt);
public sealed record ChatRoomTurnDto(Guid Id, Guid PromptMessageId, Guid RequestedByUserId, string AgentName, string Status, Guid? AiRunId, DateTime StartedAt, string? Error, IReadOnlyList<ChatRoomApproval> Approvals, bool CanClose);
public sealed record ChatRoomDetail(ChatRoomSummary Room, IReadOnlyList<ChatRoomParticipant> Participants, IReadOnlyList<RoomContextReference> Context, IReadOnlyList<ChatRoomMessageDto> Messages, IReadOnlyList<ChatRoomTurnDto> Turns, bool HasEarlier, Guid CurrentUserId);
public sealed record RoomAgentOption(string Name, string Description, string Scope);
public sealed record CreateChatRoomRequest(string Title, IReadOnlyList<RoomContextReference>? Context = null);
public sealed record AddChatRoomMemberRequest(string ActorKey, string Role, bool ShareHistory);
public sealed record SendRoomMessageRequest(Guid ClientRequestId, string Content, IReadOnlyList<string>? MentionKeys = null);
public sealed record RunRoomAgentRequest(Guid ClientRequestId, Guid PromptMessageId, string AgentName, Guid? ContinueApprovalId = null);
public sealed record DecideRoomApprovalRequest(Guid TurnId, Guid ApprovalId, string Decision, string? Reason = null);

public sealed class ChatRoomException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
