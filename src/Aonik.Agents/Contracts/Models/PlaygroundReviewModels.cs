namespace Aonik.Agents.Contracts.Models;

/// <summary>
/// Request DTO for the AI Playground review endpoint.
/// Sends the full playground conversation context to a reviewer LLM
/// that evaluates the agent's responses using RAGAS-style metrics.
/// </summary>
public sealed record PlaygroundReviewRequest
{
    /// <summary>The system prompt that was given to the agent.</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>The user brief JSON context (if provided).</summary>
    public string? UserBriefJson { get; init; }

    /// <summary>The conversation messages (user + assistant turns).</summary>
    public List<PlaygroundMessage>? Messages { get; init; }

    /// <summary>The final assistant response text to review.</summary>
    public string? AssistantResponse { get; init; }

    /// <summary>
    /// Tool calls made during the conversation, for faithfulness evaluation.
    /// </summary>
    public List<PlaygroundReviewToolCall>? ToolCalls { get; init; }

    /// <summary>AI model ID to use for the review. When null, uses the default model.</summary>
    public Guid? ModelId { get; init; }
}

/// <summary>
/// Simplified tool call record for the reviewer to evaluate faithfulness.
/// </summary>
public sealed record PlaygroundReviewToolCall
{
    public string ToolName { get; init; } = string.Empty;
    public string Arguments { get; init; } = string.Empty;
    public string? Result { get; init; }
}
