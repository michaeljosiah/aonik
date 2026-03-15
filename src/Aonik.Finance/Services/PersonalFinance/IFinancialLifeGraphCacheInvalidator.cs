using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Caching;

namespace Aonik.Finance.Services.PersonalFinance;

internal interface IFinancialLifeGraphCacheInvalidator
{
    void InvalidateCurrentUserGraph();
    Task InvalidateCurrentUserGraphAsync(CancellationToken cancellationToken = default);
    Task InvalidateAllGraphCachesAsync(CancellationToken cancellationToken = default);
}

internal sealed class FinancialLifeGraphCacheInvalidator : IFinancialLifeGraphCacheInvalidator
{
    private readonly ITenantProvider _tenantProvider;
    private readonly Aonik.SharedKernel.Abstractions.ICurrentUserProvider _currentUserProvider;
    private readonly ICacheInvalidationPublisher _cacheInvalidationPublisher;

    public FinancialLifeGraphCacheInvalidator(
        ITenantProvider tenantProvider,
        Aonik.SharedKernel.Abstractions.ICurrentUserProvider currentUserProvider,
        ICacheInvalidationPublisher cacheInvalidationPublisher)
    {
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _cacheInvalidationPublisher = cacheInvalidationPublisher;
    }

    public void InvalidateCurrentUserGraph()
    {
        InvalidateCurrentUserGraphAsync().GetAwaiter().GetResult();
    }

    public async Task InvalidateCurrentUserGraphAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            return;
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        await _cacheInvalidationPublisher.PublishAsync(
            new CacheInvalidationEvent(FinancialLifeGraphService.CacheSet, GetCacheKey(tenantId, userId)),
            cancellationToken);
    }

    public async Task InvalidateAllGraphCachesAsync(CancellationToken cancellationToken = default)
    {
        await _cacheInvalidationPublisher.PublishAsync(
            new CacheInvalidationEvent(FinancialLifeGraphService.CacheSet),
            cancellationToken);
    }

    internal static string GetCacheKey(Guid tenantId, Guid userId) => $"personal-finance:graph:{tenantId:D}:{userId:D}";
}
