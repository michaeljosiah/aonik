using Aonik.Agents.Contracts.Agui;

namespace Aonik.Agents.Contracts.Services;

/// <summary>
/// Immutable snapshot of a thread's AG-UI message history kept in the local
/// cache to avoid reconstructing every thin-client turn from the database.
/// </summary>
public sealed record ChatThreadHistorySnapshot(IReadOnlyList<AguiMessage> Messages);

/// <summary>
/// Result of resolving a cached thread history snapshot.
/// </summary>
public sealed record ChatThreadHistoryCacheLookup(
    ChatThreadHistorySnapshot Snapshot,
    bool IsCacheHit);

/// <summary>
/// Caches recent AG-UI message history per thread so follow-up turns can avoid
/// hitting persistent storage before the model stream starts.
/// </summary>
public interface IChatThreadHistoryCache
{
    Task<ChatThreadHistoryCacheLookup> GetOrLoadAsync(
        Guid threadId,
        Func<CancellationToken, Task<IReadOnlyList<AguiMessage>>> factory,
        CancellationToken cancellationToken = default);

    Task StoreAsync(
        Guid threadId,
        IReadOnlyList<AguiMessage> messages,
        CancellationToken cancellationToken = default);

    Task AppendAsync(
        Guid threadId,
        AguiMessage message,
        CancellationToken cancellationToken = default);
}
