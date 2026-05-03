using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Ai.Services;

/// <summary>
/// FusionCache-backed implementation of <see cref="ITtsCache"/>. Reads
/// and writes are gated by <see cref="TtsCacheAllowlist"/>; non-allow-
/// listed text is silently ignored so callers can invoke unconditionally
/// on the synthesis path.
/// </summary>
internal sealed class TtsCache : ITtsCache
{
    // 24 hours covers the typical lifetime of a tenant's voice settings;
    // cache entries are invalidated implicitly when the voice / model /
    // format changes (different cache key).
    private static readonly FusionCacheEntryOptions DefaultEntryOptions = new(TimeSpan.FromHours(24));

    private readonly IFusionCache _cache;

    public TtsCache(IFusionCache cache)
    {
        _cache = cache;
    }

    public bool IsAllowlisted(string normalizedText) =>
        TtsCacheAllowlist.Contains(normalizedText);

    public async ValueTask<TtsCacheEntry?> TryGetAsync(TtsCacheKey key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key.TextHash))
        {
            return null;
        }

        return await _cache.GetOrDefaultAsync<TtsCacheEntry?>(
            key.Serialize(),
            defaultValue: null,
            DefaultEntryOptions,
            cancellationToken);
    }

    public async ValueTask SetAsync(TtsCacheKey key, TtsCacheEntry entry, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key.TextHash))
        {
            return;
        }

        await _cache.SetAsync(
            key.Serialize(),
            entry,
            DefaultEntryOptions,
            cancellationToken);
    }
}
