using System.Collections.Concurrent;

namespace Aonik.Infrastructure.Caching;

public interface ICacheSetRegistry
{
    void Track(string cacheSet, string cacheKey);
    IReadOnlyCollection<string> GetKeys(string cacheSet);
    void RemoveKey(string cacheSet, string cacheKey);
}

public class CacheSetRegistry : ICacheSetRegistry
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
