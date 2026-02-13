using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Infrastructure.Caching;

public interface ICachePolicyProvider
{
    FusionCacheEntryOptions Get(CachePolicy policy);
}

public class CachePolicyProvider : ICachePolicyProvider
{
    private static readonly FusionCacheEntryOptions ShortOptions = new FusionCacheEntryOptions
    {
        Duration = TimeSpan.FromSeconds(30),
        JitterMaxDuration = TimeSpan.FromSeconds(3)
    };

    private static readonly FusionCacheEntryOptions MediumOptions = new FusionCacheEntryOptions
    {
        Duration = TimeSpan.FromMinutes(5),
        JitterMaxDuration = TimeSpan.FromSeconds(20)
    };

    private static readonly FusionCacheEntryOptions LongOptions = new FusionCacheEntryOptions
    {
        Duration = TimeSpan.FromMinutes(30),
        JitterMaxDuration = TimeSpan.FromMinutes(1)
    };

    public FusionCacheEntryOptions Get(CachePolicy policy)
    {
        return policy switch
        {
            CachePolicy.Short => ShortOptions,
            CachePolicy.Medium => MediumOptions,
            CachePolicy.Long => LongOptions,
            _ => MediumOptions
        };
    }
}
