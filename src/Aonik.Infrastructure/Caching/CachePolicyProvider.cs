using ZiggyCreatures.Caching.Fusion;
using Aonik.SharedKernel.Caching;

namespace Aonik.Infrastructure.Caching;

/// <summary>
/// Maps the well-known <see cref="CachePolicy"/> tiers (Short / Medium /
/// Long) onto the concrete <see cref="FusionCacheEntryOptions"/> used by
/// FusionCache. Concrete class injected directly — the
/// <c>ICachePolicyProvider</c> interface that previously fronted this
/// class was a single-impl wrapper with no test double or alternate
/// implementation. Deleted by the 2026-05-05 single-impl audit.
/// </summary>
public class CachePolicyProvider
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
