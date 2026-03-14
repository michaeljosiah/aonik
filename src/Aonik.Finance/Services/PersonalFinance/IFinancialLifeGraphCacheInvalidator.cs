using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.Extensions.Caching.Memory;

namespace Aonik.Finance.Services.PersonalFinance;

internal interface IFinancialLifeGraphCacheInvalidator
{
    void InvalidateCurrentUserGraph();
}

internal sealed class FinancialLifeGraphCacheInvalidator : IFinancialLifeGraphCacheInvalidator
{
    private readonly ITenantProvider _tenantProvider;
    private readonly Aonik.SharedKernel.Abstractions.ICurrentUserProvider _currentUserProvider;
    private readonly IMemoryCache _memoryCache;

    public FinancialLifeGraphCacheInvalidator(
        ITenantProvider tenantProvider,
        Aonik.SharedKernel.Abstractions.ICurrentUserProvider currentUserProvider,
        IMemoryCache memoryCache)
    {
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _memoryCache = memoryCache;
    }

    public void InvalidateCurrentUserGraph()
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            return;
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        _memoryCache.Remove(GetCacheKey(tenantId, userId));
    }

    internal static string GetCacheKey(Guid tenantId, Guid userId) => $"personal-finance:graph:{tenantId:D}:{userId:D}";
}
