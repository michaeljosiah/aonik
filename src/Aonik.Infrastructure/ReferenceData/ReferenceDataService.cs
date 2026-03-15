using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Platform.Contracts.Services.ReferenceData;
using Aonik.Platform.Contracts.Models.ReferenceData;
using Aonik.Platform.Entities.ReferenceData;
using Aonik.Infrastructure.Caching;
using Aonik.SharedKernel.Caching;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Infrastructure.ReferenceData;

public class ReferenceDataService : IReferenceDataService
{
    private const string CacheSet = "reference-data";
    private readonly IAonikDbContext _dbContext;
    private readonly ICacheStore _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICacheInvalidationPublisher _cacheInvalidationPublisher;

    public ReferenceDataService(
        IAonikDbContext dbContext,
        ICacheStore cache,
        ITenantProvider tenantProvider,
        ICacheInvalidationPublisher cacheInvalidationPublisher)
    {
        _dbContext = dbContext;
        _cache = cache;
        _tenantProvider = tenantProvider;
        _cacheInvalidationPublisher = cacheInvalidationPublisher;
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

        var cached = await _cache.GetOrSetAsync(
            cacheKey,
            CachePolicy.Medium,
            async ct =>
            {
                var items = await _dbContext.ReferenceDataItems
                    .AsNoTracking()
                    .Where(item => item.Type == normalizedType && (item.TenantId == null || item.TenantId == tenantId))
                    .ToListAsync(ct);

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

                return (IReadOnlyList<ReferenceDataItemSnapshot>)resolved;
            },
            CacheSet,
            cancellationToken);

        return cached ?? [];
    }

    public async Task<ReferenceDataItemSnapshot> UpsertAsync(
        ReferenceDataItemUpsert request,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Type))
        {
            throw new ArgumentException("Reference data type is required.", nameof(request.Type));
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new ArgumentException("Reference data code is required.", nameof(request.Code));
        }

        var normalizedType = request.Type.Trim();
        var normalizedCode = request.Code.Trim();
        tenantId ??= _tenantProvider.TryGetCurrentTenantId(out var resolvedTenantId) ? resolvedTenantId : null;

        var item = await _dbContext.ReferenceDataItems
            .FirstOrDefaultAsync(
                existing => existing.Type == normalizedType
                    && existing.Code == normalizedCode
                    && existing.TenantId == tenantId,
                cancellationToken);

        if (item == null)
        {
            item = new ReferenceDataItem
            {
                Id = Guid.NewGuid(),
                Type = normalizedType,
                Code = normalizedCode,
                DisplayName = request.DisplayName.Trim(),
                SortOrder = request.SortOrder,
                IsActive = request.IsActive,
                TenantId = tenantId
            };

            _dbContext.ReferenceDataItems.Add(item);
        }
        else
        {
            item.DisplayName = request.DisplayName.Trim();
            item.SortOrder = request.SortOrder;
            item.IsActive = request.IsActive;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await EvictCacheAsync(normalizedType, tenantId, cancellationToken);

        return Map(item);
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

    private async Task EvictCacheAsync(string type, Guid? tenantId, CancellationToken cancellationToken)
    {
        await _cacheInvalidationPublisher.PublishAsync(new CacheInvalidationEvent(CacheSet, GetCacheKey(type, tenantId)), cancellationToken);
        await _cacheInvalidationPublisher.PublishAsync(new CacheInvalidationEvent(CacheSet, GetCacheKey(type, null)), cancellationToken);

        if (_tenantProvider.TryGetCurrentTenantId(out var currentTenantId))
        {
            await _cacheInvalidationPublisher.PublishAsync(new CacheInvalidationEvent(CacheSet, GetCacheKey(type, currentTenantId)), cancellationToken);
        }
    }
}
