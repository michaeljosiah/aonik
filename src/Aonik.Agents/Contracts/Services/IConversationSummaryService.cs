namespace Aonik.Agents.Contracts.Services;

/// <summary>
/// Generates conversation summaries from completed chat threads.
/// Public contract for cross-module consumption (e.g., Worker project).
/// </summary>
public interface IConversationSummaryService
{
    /// <summary>
    /// Generates a summary for the given chat thread. Idempotent.
    /// </summary>
    Task GenerateSummaryAsync(Guid chatThreadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds stale active sessions that need summarisation and generates summaries for them.
    /// </summary>
    Task ProcessStaleSessionsAsync(int batchSize = 10, CancellationToken cancellationToken = default);
}
