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
    bool IsNewThread,
    string? FirstUserMessage,
    string ThreadIdString);

/// <summary>
/// Runs thread-message persistence and title generation after the response has been flushed. Uses a
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
