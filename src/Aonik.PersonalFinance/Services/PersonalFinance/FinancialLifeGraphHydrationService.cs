using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Caching;

namespace Aonik.PersonalFinance.Services;

internal sealed class FinancialLifeGraphHydrationService
{
    internal const string CoreCacheSet = "personal-finance-graph";
    internal const string FxCacheSet = "personal-finance-graph-fx";
    internal const int TransactionWindowDays = 120;
    internal const int WarningThresholdCount = 1000;

    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICacheStore _cacheStore;
    private readonly FinancialLifeGraphLoader _loader;
    private readonly FinancialLifeGraphSnapshotMetrics _snapshotMetrics;

    public FinancialLifeGraphHydrationService(
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        ICacheStore cacheStore,
        FinancialLifeGraphLoader loader,
        FinancialLifeGraphSnapshotMetrics snapshotMetrics)
    {
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _cacheStore = cacheStore;
        _loader = loader;
        _snapshotMetrics = snapshotMetrics;
    }

    public async Task<FinancialLifeGraphSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();
        var coreCacheKey = FinancialLifeGraphCacheInvalidator.GetCoreCacheKey(tenantId, userId);

        var coreSnapshot = await _cacheStore.GetOrSetAsync(
            coreCacheKey,
            CachePolicy.Medium,
            async ct => await _loader.LoadCoreSnapshotAsync(tenantId, userId, TransactionWindowDays, ct),
            CoreCacheSet,
            cancellationToken)
            ?? await _loader.LoadCoreSnapshotAsync(tenantId, userId, TransactionWindowDays, cancellationToken);

        var relevantAccountCurrencies = FinancialLifeGraphLoader.GetRelevantAccountCurrencies(coreSnapshot.Accounts, coreSnapshot.LinkedAccounts);
        var fxCacheKey = FinancialLifeGraphCacheInvalidator.GetFxCacheKey(tenantId, userId);
        var fxQuotes = await _cacheStore.GetOrSetAsync(
            fxCacheKey,
            CachePolicy.Short,
            async ct => await _loader.LoadFxQuotesAsync(tenantId, relevantAccountCurrencies, ct),
            FxCacheSet,
            cancellationToken)
            ?? await _loader.LoadFxQuotesAsync(tenantId, relevantAccountCurrencies, cancellationToken);

        _snapshotMetrics.LogSnapshotLoaded(
            tenantId,
            userId,
            coreSnapshot.Transactions.Count,
            coreSnapshot.Bills.Count,
            coreSnapshot.Goals.Count,
            coreSnapshot.Subscriptions.Count,
            coreSnapshot.NativeNodes.Count,
            coreSnapshot.NativeEdges.Count,
            coreSnapshot.Transactions.FirstOrDefault()?.OccurredAt,
            fxQuotes.Count,
            relevantAccountCurrencies.Count,
            CountFundingRelationships(coreSnapshot),
            coreSnapshot.NativeNodes.Count(item => item.IsInferred));

        return coreSnapshot with { FxQuotes = fxQuotes };
    }

    private int CountFundingRelationships(FinancialLifeGraphSnapshot snapshot)
    {
        return snapshot.Bills.Count(item => item.PaidFromAccountId.HasValue)
            + snapshot.Goals.Count(item => item.FundingAccountId.HasValue);
    }

    private Guid GetCurrentUserId()
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        return userId;
    }
}
