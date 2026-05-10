namespace Aonik.Agents.Contracts.Services;

/// <summary>
/// Captures everything a post-stream persistence run needs. Built inside the
/// request scope after the response has been flushed, then handed to the
/// coordinator which runs persistence in a detached background scope.
/// </summary>
public sealed record PostStreamPersistenceContext(
    Guid? PersistedThreadId,
    Guid? TenantId,
    Guid? UserId,
    string AssistantText,
    string? AgentId,
    long InputTokens,
    long OutputTokens,
    long LatencyMs,
    bool IsNewThread,
    string? FirstUserMessage,
    string ThreadIdString,
    string RunId,
    string? UseCase = null);
//   ^ UseCase added by Aonik.Voice (spec docs/specifications/022.aonik-voice-realtime.md
//   Phase 3 / "Post-Turn Persistence And AiRun Rows"). Voice supplies "voice"; AGUI keeps
//   the legacy AgentId-derived behavior by leaving it null.

/// <summary>
/// Runs thread-message persistence, title generation, and AiRun metrics
/// writing *after* the AG-UI response has been flushed to the wire. Uses a
/// fresh DI scope with tenant + user context re-seeded so writes hit the
/// correct tenant without holding up the request thread.
/// </summary>
public interface IPostStreamPersistenceCoordinator
{
    /// <summary>
    /// Fire-and-forget: schedules the persistence work and returns immediately.
    /// Errors are logged but never thrown, since the caller has already
    /// returned the HTTP response.
    /// </summary>
    void Enqueue(PostStreamPersistenceContext context);
}
