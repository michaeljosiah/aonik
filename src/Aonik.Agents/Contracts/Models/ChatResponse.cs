namespace Aonik.Agents.Contracts.Models;

/// <summary>
/// Response model from the AI chat endpoint.
/// </summary>
public sealed record AgentChatResponse
{
    /// <summary>The agent's response text.</summary>
    public required string Message { get; init; }

    /// <summary>Session ID for continuing the conversation.</summary>
    public required string SessionId { get; init; }

    /// <summary>Name of the agent that produced the final response.</summary>
    public string? AgentName { get; init; }

    /// <summary>
    /// Persisted thread ID for conversation history. Use this to continue
    /// the conversation in subsequent requests.
    /// </summary>
    public string? ThreadId { get; init; }

    /// <summary>AI-generated title for the conversation thread.</summary>
    public string? ThreadTitle { get; init; }
}
