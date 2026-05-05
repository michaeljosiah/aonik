using System.Collections.Concurrent;

namespace Aonik.Infrastructure.Caching;

/// <summary>
/// Tracks (cache-set, cache-key) memberships so the cache-management
/// admin endpoint can invalidate every key in a set. Concrete class
/// injected directly — the <c>ICacheSetRegistry</c> interface that
/// previously fronted this class was a single-impl wrapper with no test
/// double or alternate implementation. Deleted by the 2026-05-05
/// single-impl audit.
/// </summary>
public class CacheSetRegistry
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _sets = new(StringComparer.OrdinalIgnoreCase);

    public void Track(string cacheSet, string cacheKey)
    {
        var cacheKeys = _sets.GetOrAdd(cacheSet, static _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
        cacheKeys.TryAdd(cacheKey, 0);
    }

    public IReadOnlyCollection<string> GetKeys(string cacheSet)
    {
        if (!_sets.TryGetValue(cacheSet, out var keys))
        {
            return [];
        }

        return keys.Keys.ToArray();
    }

    public IReadOnlyCollection<string> GetCacheSets()
    {
        return _sets.Keys.ToArray();
    }

    public void RemoveKey(string cacheSet, string cacheKey)
    {
        if (_sets.TryGetValue(cacheSet, out var keys))
        {
            keys.TryRemove(cacheKey, out _);
            if (keys.IsEmpty)
            {
                _sets.TryRemove(cacheSet, out _);
            }
        }
    }
}
