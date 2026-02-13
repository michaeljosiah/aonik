namespace Aonik.Infrastructure.Caching;

public record CacheInvalidationEvent(
    string CacheSet,
    string? CacheKey = null);
