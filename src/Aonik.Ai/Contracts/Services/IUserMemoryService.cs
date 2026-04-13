using Aonik.Ai.Entities;

namespace Aonik.Ai.Contracts.Services;

/// <summary>
/// Manages user memory entries — the key-value store for anything the AI
/// learns about a user that doesn't belong in a domain entity.
/// </summary>
public interface IUserMemoryService
{
    /// <summary>
    /// Creates or supersedes a memory entry for the given key.
    /// If an active entry with the same key exists, it is superseded (not deleted).
    /// </summary>
    Task<UserMemoryEntryResponse> SetEntryAsync(
        SetUserMemoryEntryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all current (non-superseded) entries for a user, optionally filtered by type.
    /// Applies confidence decay for AI-inferred entries and excludes entries below the confidence floor.
    /// </summary>
    Task<IReadOnlyList<UserMemoryEntryResponse>> GetCurrentEntriesAsync(
        Guid userId,
        UserMemoryEntryType? entryType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the correction/supersede history for a specific key.
    /// </summary>
    Task<IReadOnlyList<UserMemoryEntryResponse>> GetEntryHistoryAsync(
        Guid userId,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms an existing entry, resetting its LastConfirmedAt to now.
    /// Used to prevent confidence decay on AI-inferred entries that are still valid.
    /// </summary>
    Task ConfirmEntryAsync(
        Guid entryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Semantically search user memory entries using a natural language query.
    /// Returns entries ranked by relevance. Returns an empty list if the backend
    /// does not support semantic search (e.g. SQL Server without vector embeddings).
    /// </summary>
    Task<IReadOnlyList<SemanticMemorySearchResult>> SemanticSearchAsync(
        Guid userId,
        string query,
        int limit = 5,
        float scoreThreshold = 0.6f,
        CancellationToken cancellationToken = default);
}

public record SetUserMemoryEntryRequest(
    Guid UserId,
    UserMemoryEntryType EntryType,
    string Key,
    string ValueJson,
    decimal Confidence,
    UserMemorySource Source,
    Guid? AiRunId = null);

public record UserMemoryEntryResponse(
    Guid Id,
    Guid UserId,
    UserMemoryEntryType EntryType,
    string Key,
    string ValueJson,
    decimal Confidence,
    decimal EffectiveConfidence,
    UserMemorySource Source,
    Guid? AiRunId,
    Guid? SupersededById,
    DateTime CreatedAt,
    DateTime LastConfirmedAt);

public record SemanticMemorySearchResult(
    UserMemoryEntryResponse Entry,
    float RelevanceScore);
