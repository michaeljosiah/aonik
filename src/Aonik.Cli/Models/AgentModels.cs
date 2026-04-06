namespace Aonik.Cli.Models;

public sealed record AgentInfo(
    string Name,
    string Description);

public sealed record ListAgentsResponse(
    IReadOnlyList<AgentInfo> Agents);

public sealed record ChatRequest(
    string Message,
    string? SessionId,
    string? ThreadId);

public sealed record AgentStreamRequest(
    string Message,
    string? ThreadId,
    string? RunId,
    string? AgentId);

public sealed record AgentStreamEvent(
    string Type,
    string Json,
    string? Name = null);

public sealed record AgentChatResponse(
    string Message,
    string SessionId,
    string? AgentName,
    string? ThreadId,
    string? ThreadTitle);

public sealed record ChatThreadSummary(
    Guid Id,
    string Title,
    string Status,
    string? AgentName,
    DateTime? LastMessageAt,
    int MessageCount,
    DateTime CreatedAt);

public sealed record ChatThreadDetail(
    Guid Id,
    string Title,
    string Status,
    string? AgentName,
    DateTime? LastMessageAt,
    int MessageCount,
    DateTime CreatedAt,
    List<ChatThreadMessageDto> Messages);

public sealed record ChatThreadMessageDto(
    Guid Id,
    string Role,
    string Content,
    string? AgentName,
    string? ToolCallsJson,
    int SortOrder,
    DateTime CreatedAt);

public sealed record ListChatThreadsResponse(
    IReadOnlyList<ChatThreadSummary> Threads,
    int Page,
    int PageSize);

public sealed record RunAgentOptions(
    string Message,
    string? SessionId,
    string? ThreadId,
    OutputMode OutputMode);

public sealed record StreamAgentOptions(
    string Message,
    string? ThreadId,
    string? RunId,
    string? AgentId,
    OutputMode OutputMode);

public sealed record ListThreadsOptions(
    int Page,
    int PageSize,
    OutputMode OutputMode);

public sealed record WorkflowRequest(
    string WorkflowName,
    string Input);

public sealed record WorkflowResponse(
    string WorkflowName,
    string Output,
    bool Success,
    string? Error);
