namespace Aonik.Api.Contracts.Settings;

public record CacheSetSummaryResponse(
    string Name,
    int EntryCount);

public record CacheOverviewResponse(
    IReadOnlyCollection<CacheSetSummaryResponse> CacheSets,
    int TotalCacheSets,
    int TotalEntries);

public record InvalidateCacheSetRequest(
    string CacheSet);

public record InvalidateCacheSetResponse(
    string CacheSet,
    bool Invalidated,
    DateTimeOffset InvalidatedAtUtc);
