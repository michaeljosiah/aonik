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
/// <remarks>
/// Cache keys are tenant-prefixed (<c>agui:thread-history:v1:{tenantId}:{threadId}</c>).
/// The <paramref name="tenantId"/> argument is required and MUST match the
/// tenant the thread belongs to: a thread GUID is unique enough that two
/// tenants will never collide today, but tenant prefixing prevents a future
/// dataset migration / merge from cross-pollinating one tenant's
/// FusionCache entry with another's. Pass <see cref="Guid.Empty"/> for
/// platform-level threads (rare).
/// </remarks>
public interface IChatThreadHistoryCache
{
    Task<ChatThreadHistoryCacheLookup> GetOrLoadAsync(
        Guid tenantId,
        Guid threadId,
        Func<CancellationToken, Task<IReadOnlyList<AguiMessage>>> factory,
        CancellationToken cancellationToken = default);

    Task StoreAsync(
        Guid tenantId,
        Guid threadId,
        IReadOnlyList<AguiMessage> messages,
        CancellationToken cancellationToken = default);

    Task AppendAsync(
        Guid tenantId,
        Guid threadId,
        AguiMessage message,
        CancellationToken cancellationToken = default);
}
