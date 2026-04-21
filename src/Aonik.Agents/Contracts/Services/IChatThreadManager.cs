using Aonik.Agents.Contracts.Agui;

namespace Aonik.Agents.Contracts.Services;

/// <summary>
/// Context returned by <see cref="IChatThreadManager.EnsureThreadAsync"/>
/// describing the thread the AG-UI turn is running against.
/// </summary>
/// <param name="PersistedThreadId">
/// The GUID of the persisted thread, or <c>null</c> when persistence is
/// disabled (no <c>IChatThreadService</c> registered) or the turn has no
/// user message to anchor a thread on.
/// </param>
/// <param name="ThreadIdString">
/// The threadId that must appear in AG-UI SSE events. When a new thread is
/// created, this is the persisted GUID in "N" format; otherwise it's the
/// client-supplied value (or a new non-GUID for guest turns).
/// </param>
/// <param name="IsNewThread">
/// True when this turn created the thread. Controls whether title generation
/// runs in the post-stream persistence step.
/// </param>
/// <param name="FirstUserMessage">
/// The latest user message content from the incoming AG-UI messages. Used
/// as the title-generation seed for new threads.
/// </param>
public readonly record struct ChatThreadContext(
    Guid? PersistedThreadId,
    string ThreadIdString,
    bool IsNewThread,
    string? FirstUserMessage);

/// <summary>
/// Result of reconstructing AG-UI history for a streaming turn.
/// </summary>
/// <param name="Messages">The effective messages to feed into the agent.</param>
/// <param name="Source">
/// Where the history came from: <c>client</c>, <c>cache</c>, or <c>db</c>.
/// </param>
/// <param name="DurationMs">Time spent resolving the effective history.</param>
public readonly record struct ChatHistoryResolution(
    IReadOnlyList<AguiMessage>? Messages,
    string Source,
    long DurationMs);

/// <summary>
/// Manages persisted chat thread lifecycle for the AG-UI streaming endpoint:
/// thread creation/lookup, detached user-message append, and thin-client
/// history reconstruction.
/// </summary>
public interface IChatThreadManager
{
    /// <summary>
    /// Resolves the persisted thread for this turn. For existing threads,
    /// schedules the user-message append as fire-and-forget off the critical
    /// streaming path. For new threads, creates the thread inline so its ID
    /// is available for subsequent SSE events.
    /// </summary>
    Task<ChatThreadContext> EnsureThreadAsync(
        string? clientThreadId,
        IReadOnlyList<AguiMessage>? messages,
        string? agentId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Thin-client optimisation: when the request carries a persisted thread
    /// GUID and only the new user turn, reconstructs prior history from the
    /// persisted thread. Falls back to the client-supplied messages unchanged
    /// when conditions are not met or on any retrieval failure.
    /// </summary>
    Task<ChatHistoryResolution> ReconstructHistoryAsync(
        Guid? persistedThreadId,
        IReadOnlyList<AguiMessage>? clientMessages,
        CancellationToken cancellationToken);
}
