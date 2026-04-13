namespace Aonik.SharedKernel.Abstractions.Ai;

/// <summary>
/// Cross-module abstraction for persisting user memory entries.
/// Used by agent tools (Agents module) and the conversation summary generator
/// to write learned facts, preferences, corrections, and identity data.
/// Implemented by the AI module.
/// </summary>
public interface IUserMemorySaveProvider
{
    /// <summary>
    /// Creates or supersedes a memory entry for the given key.
    /// If an active entry with the same key exists, it is superseded (not deleted).
    /// </summary>
    Task<UserMemorySaveResult> SaveAsync(
        UserMemorySaveRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request to save a user memory entry.
/// </summary>
/// <param name="UserId">The user this memory belongs to.</param>
/// <param name="EntryType">Category: Identity, Preference, Correction, or Fact.</param>
/// <param name="Key">Namespaced key (e.g. "finance.preferred_pay_day", "identity.household_size").</param>
/// <param name="ValueJson">Schema-agnostic JSON value.</param>
/// <param name="Confidence">1.0 for user-stated, 0.7-0.8 for AI-inferred.</param>
/// <param name="Source">How the entry was derived: UserStated, AiInferred, or SystemDerived.</param>
public record UserMemorySaveRequest(
    Guid UserId,
    string EntryType,
    string Key,
    string ValueJson,
    decimal Confidence,
    string Source);

/// <summary>
/// Result of a memory save operation.
/// </summary>
/// <param name="EntryId">The ID of the created/updated entry.</param>
/// <param name="Key">The key that was saved.</param>
/// <param name="WasSuperseded">True if an existing entry was superseded.</param>
public record UserMemorySaveResult(
    Guid EntryId,
    string Key,
    bool WasSuperseded);
