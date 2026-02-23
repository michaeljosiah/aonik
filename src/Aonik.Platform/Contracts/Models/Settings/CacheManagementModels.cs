namespace Aonik.Platform.Contracts.Models.Settings;

public record CacheSetSummary(
    string Name,
    int EntryCount);

public record CacheOverviewResponse(
    IReadOnlyCollection<CacheSetSummary> CacheSets,
    int TotalCacheSets,
    int TotalEntries);

public record InvalidateCacheSetResponse(
    string CacheSet,
    bool Invalidated,
    DateTimeOffset InvalidatedAtUtc);
