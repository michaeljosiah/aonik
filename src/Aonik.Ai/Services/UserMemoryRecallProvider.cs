using Aonik.Ai.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Ai;

namespace Aonik.Ai.Services;

/// <summary>
/// Implements the cross-module <see cref="IUserMemoryRecallProvider"/> contract
/// by delegating to the active <see cref="IUserMemoryService"/> implementation.
/// </summary>
internal sealed class UserMemoryRecallProvider : IUserMemoryRecallProvider
{
    private readonly IUserMemoryService _memoryService;

    public UserMemoryRecallProvider(IUserMemoryService memoryService)
    {
        _memoryService = memoryService;
    }

    public async Task<IReadOnlyList<UserMemoryRecallResult>> RecallAsync(
        Guid userId,
        string query,
        int limit = 5,
        float scoreThreshold = 0.6f,
        CancellationToken cancellationToken = default)
    {
        var results = await _memoryService.SemanticSearchAsync(
            userId, query, limit, scoreThreshold, cancellationToken);

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
}
