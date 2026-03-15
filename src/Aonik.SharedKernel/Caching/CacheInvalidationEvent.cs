namespace Aonik.SharedKernel.Caching;

public record CacheInvalidationEvent(
    string CacheSet,
    string? CacheKey = null);
