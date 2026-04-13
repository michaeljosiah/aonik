namespace Aonik.SharedKernel.Abstractions.Ai;

/// <summary>
/// Cross-module abstraction for semantically searching user memory entries.
/// Used by agent tools (Agents module) to recall contextual user information
/// mid-conversation. Implemented by the AI module.
/// </summary>
public interface IUserMemoryRecallProvider
{
    /// <summary>
    /// Semantically search user memory entries using a natural language query.
    /// Returns entries ranked by relevance. Returns an empty list if the
    /// active backend does not support semantic search.
    /// </summary>
    Task<IReadOnlyList<UserMemoryRecallResult>> RecallAsync(
        Guid userId,
        string query,
        int limit = 5,
        float scoreThreshold = 0.6f,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A single user memory recall result with relevance score.
/// </summary>
public record UserMemoryRecallResult(
    string Key,
    string EntryType,
    string ValueJson,
    decimal EffectiveConfidence,
    string Source,
    float RelevanceScore,
    DateTime LastConfirmedAt);
