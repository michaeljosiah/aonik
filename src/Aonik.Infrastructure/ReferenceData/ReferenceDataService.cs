using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Abstractions.ReferenceData;
using Aonik.Application.Models.ReferenceData;
using Aonik.Domain.ReferenceData.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Aonik.Infrastructure.ReferenceData;

public class ReferenceDataService : IReferenceDataService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private readonly IAonikDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly ITenantProvider _tenantProvider;

    public ReferenceDataService(
        IAonikDbContext dbContext,
        IMemoryCache cache,
        ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _cache = cache;
        _tenantProvider = tenantProvider;
    }

    public async Task<IReadOnlyList<ReferenceDataItemSnapshot>> GetAsync(
        string type,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Reference data type is required.", nameof(type));
        }

        var normalizedType = type.Trim();
        tenantId ??= _tenantProvider.TryGetCurrentTenantId(out var resolvedTenantId) ? resolvedTenantId : null;

        var cacheKey = GetCacheKey(normalizedType, tenantId);
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<ReferenceDataItemSnapshot>? cached) && cached is not null)
        {
            return cached;
        }

        var items = await _dbContext.ReferenceDataItems
            .AsNoTracking()
            .Where(item => item.Type == normalizedType && (item.TenantId == null || item.TenantId == tenantId))
            .ToListAsync(cancellationToken);

        var resolved = items
            .GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(item => item.TenantId.HasValue)
                .ThenBy(item => item.SortOrder)
                .First())
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.DisplayName)
            .Select(Map)
            .ToList();

        _cache.Set(cacheKey, resolved, CacheTtl);
        return resolved;
    }

    private static string GetCacheKey(string type, Guid? tenantId)
    {
        return $"reference-data:{tenantId}:{type}";
    }

    private static ReferenceDataItemSnapshot Map(ReferenceDataItem item)
    {
        return new ReferenceDataItemSnapshot(
            item.Type,
            item.Code,
            item.DisplayName,
            item.SortOrder,
            item.IsActive);
    }
}
