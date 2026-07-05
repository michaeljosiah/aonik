namespace Aonik.PersonalFinance.Services;

/// <summary>
/// PersonalFinance graph-cache invalidator. Defined in <c>Aonik.PersonalFinance</c>
/// so consumers in this module can reference it; the implementation
/// (<c>FinancialLifeGraphCacheInvalidator</c>) lives in <c>Aonik.Finance</c>
/// until <see cref="FinancialLifeGraphHydrationService"/> migrates here
/// (Spec 027 Phase 3 remainder).
/// </summary>
internal interface IFinancialLifeGraphCacheInvalidator
{
    void InvalidateCurrentUserGraph();
    Task InvalidateCurrentUserGraphAsync(CancellationToken cancellationToken = default);
    Task InvalidateUserGraphAsync(Guid userId, CancellationToken cancellationToken = default);
    Task InvalidateUserGraphsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);
    Task InvalidateAllGraphCachesAsync(CancellationToken cancellationToken = default);
}
