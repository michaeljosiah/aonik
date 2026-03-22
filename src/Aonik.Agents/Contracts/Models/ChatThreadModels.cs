namespace Aonik.Agents.Contracts.Models;

/// <summary>
/// Response DTO for a chat thread summary (used in list views).
/// </summary>
public sealed record ChatThreadSummary
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Status { get; init; }
    public string? AgentName { get; init; }
    public DateTime? LastMessageAt { get; init; }
    public int MessageCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Response DTO for a chat thread with its messages.
/// </summary>
public sealed record ChatThreadDetail
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Status { get; init; }
    public string? AgentName { get; init; }
    public DateTime? LastMessageAt { get; init; }
    public int MessageCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public required List<ChatThreadMessageDto> Messages { get; init; }
}

/// <summary>
/// Response DTO for a single message within a thread.
/// </summary>
public sealed record ChatThreadMessageDto
{
    public required Guid Id { get; init; }
    public required string Role { get; init; }
    public required string Content { get; init; }
    public string? AgentName { get; init; }
    public string? ToolCallsJson { get; init; }
    public int SortOrder { get; init; }
    public DateTime CreatedAt { get; init; }
}
