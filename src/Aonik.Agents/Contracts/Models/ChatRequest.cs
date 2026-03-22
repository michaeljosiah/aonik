namespace Aonik.Agents.Contracts.Models;

/// <summary>
/// Request model for the AI chat endpoint.
/// </summary>
public sealed record ChatRequest
{
    /// <summary>The user's message to send to the orchestrator agent.</summary>
    public required string Message { get; init; }

    /// <summary>
    /// Optional session ID for multi-turn conversations. If not provided,
    /// a new session is started.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Optional thread ID for persisted conversation history. Maps to a
    /// <c>ChatThread</c> record. If not provided, a new thread is created.
    /// </summary>
    public string? ThreadId { get; init; }
}
