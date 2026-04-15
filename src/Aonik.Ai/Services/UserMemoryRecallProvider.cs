using Aonik.Ai.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.Extensions.Logging;

namespace Aonik.Ai.Services;

/// <summary>
/// Implements the cross-module <see cref="IUserMemoryRecallProvider"/> contract
/// by delegating to the active <see cref="IUserMemoryService"/> implementation.
/// </summary>
internal sealed class UserMemoryRecallProvider : IUserMemoryRecallProvider
{
    // Recall sits on the agent hot path — if the vector store is unreachable,
    // the agent must fall back fast rather than burn the full HTTP client
    // timeout (which can reach tens of seconds and visibly stalls chat).
    private static readonly TimeSpan RecallTimeout = TimeSpan.FromSeconds(2);

    private readonly IUserMemoryService _memoryService;
    private readonly ILogger<UserMemoryRecallProvider> _logger;

    public UserMemoryRecallProvider(
        IUserMemoryService memoryService,
        ILogger<UserMemoryRecallProvider> logger)
    {
        _memoryService = memoryService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UserMemoryRecallResult>> RecallAsync(
        Guid userId,
        string query,
        int limit = 5,
        float scoreThreshold = 0.6f,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(RecallTimeout);

        try
        {
            var results = await _memoryService.SemanticSearchAsync(
                userId, query, limit, scoreThreshold, timeoutCts.Token);

            return results
                .Select(r => new UserMemoryRecallResult(
                    r.Entry.Key,
                    r.Entry.EntryType.ToString(),
                    r.Entry.ValueJson,
                    r.Entry.EffectiveConfidence,
                    r.Entry.Source.ToString(),
                    r.RelevanceScore,
                    r.Entry.LastConfirmedAt))
                .ToList();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "User memory recall timed out after {TimeoutMs}ms for user {UserId} — returning empty results",
                RecallTimeout.TotalMilliseconds, userId);
            return Array.Empty<UserMemoryRecallResult>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "User memory recall failed for user {UserId} — returning empty results to avoid blocking agent",
                userId);
            return Array.Empty<UserMemoryRecallResult>();
        }
    }
}
