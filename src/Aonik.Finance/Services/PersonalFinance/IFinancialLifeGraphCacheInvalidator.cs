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
            new CacheInvalidationEvent(FinancialLifeGraphHydrationService.CoreCacheSet, GetCoreCacheKey(tenantId, userId)),
            cancellationToken);

        await _cacheInvalidationPublisher.PublishAsync(
            new CacheInvalidationEvent(FinancialLifeGraphHydrationService.FxCacheSet, GetFxCacheKey(tenantId, userId)),
            cancellationToken);
    }

    public async Task InvalidateAllGraphCachesAsync(CancellationToken cancellationToken = default)
    {
        await _cacheInvalidationPublisher.PublishAsync(
            new CacheInvalidationEvent(FinancialLifeGraphHydrationService.CoreCacheSet),
            cancellationToken);

        await _cacheInvalidationPublisher.PublishAsync(
            new CacheInvalidationEvent(FinancialLifeGraphHydrationService.FxCacheSet),
            cancellationToken);
    }

    internal static string GetCoreCacheKey(Guid tenantId, Guid userId) => $"personal-finance:graph:{tenantId:D}:{userId:D}";

    internal static string GetFxCacheKey(Guid tenantId, Guid userId) => $"personal-finance:graph:fx:{tenantId:D}:{userId:D}";
}
