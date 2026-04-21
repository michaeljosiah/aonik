using Aonik.Agents.Contracts.Models;

namespace Aonik.Agents.Contracts.Services;

/// <summary>
/// Service for managing persisted chat threads and their messages.
/// </summary>
public interface IChatThreadService
{
    /// <summary>
    /// Creates a new chat thread and appends the first user message.
    /// Returns the thread ID (which can be used as the AG-UI threadId / session ID).
    /// </summary>
    Task<Guid> CreateThreadAsync(
        string firstMessage,
        string? agentName = null,
        Guid? preferredThreadId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a message to an existing thread.
    /// </summary>
    Task AppendMessageAsync(
        Guid threadId,
        string role,
        string content,
        string? agentName = null,
        Guid? aiRunId = null,
        string? toolCallsJson = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the title of a thread (typically after AI summarisation).
    /// </summary>
    Task UpdateTitleAsync(
        Guid threadId,
        string title,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a thread with all its messages.
    /// </summary>
    Task<ChatThreadDetail?> GetThreadAsync(
        Guid threadId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns paginated thread summaries for the current user, ordered by most recent.
    /// </summary>
    Task<List<ChatThreadSummary>> ListThreadsAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives a thread (soft state change, not deletion).
    /// </summary>
    Task<bool> ArchiveThreadAsync(
        Guid threadId,
        CancellationToken cancellationToken = default);
}
