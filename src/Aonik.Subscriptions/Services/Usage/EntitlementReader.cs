using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Subscriptions.Entities.Catalogue;
using Aonik.Subscriptions.Persistence;
using Aonik.Subscriptions.Services.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Subscriptions.Services.Usage;

/// <summary>
/// Spec 087 §14.1 — what a subscriber currently holds.
///
/// Reports the last <b>settled</b> state: a pending plan change never shows here, because a
/// subscriber must not see capability they have not paid for.
/// </summary>
internal sealed class EntitlementReader : IEntitlementReader
{
    private readonly SubscriptionsDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly SubscriberAuthorization _authorization;
    private readonly IClock _clock;

    public EntitlementReader(
        SubscriptionsDbContext dbContext,
        ITenantProvider tenantProvider,
        SubscriberAuthorization authorization,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<EntitlementSnapshot?> GetAsync(
        SubscriberRef subscriber,
        CancellationToken cancellationToken = default)
    {
        await _authorization.EnsureCanActForAsync(subscriber, cancellationToken);

        var tenantId = _tenantProvider.GetCurrentTenantId();

        var subscription = await _dbContext.Subscriptions.AsNoTracking()
            .Where(s => s.TenantId == tenantId
                        && s.SubscriberKind == subscriber.Kind
                        && s.SubscriberId == subscriber.Id
                        && SubscriptionStatuses.OccupiesActiveSlot.Contains(s.Status))
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription is null)
            return null;

        // The PINNED version, never the pending one.
        var version = await _dbContext.PlanVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == subscription.PlanVersionId, cancellationToken);

        var plan = version is null
            ? null
            : await _dbContext.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == version.PlanId, cancellationToken);

        var meters = await BuildMetersAsync(tenantId, subscriber, subscription.PlanVersionId, cancellationToken);

        return new EntitlementSnapshot(
            subscriber,
            subscription.Id,
            plan?.Code ?? string.Empty,
            plan?.Name ?? string.Empty,
            subscription.PlanVersionId,
            subscription.Status,
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            meters);
    }

    public async Task<MeterEntitlement?> GetMeterAsync(
        SubscriberRef subscriber,
        string meterCode,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetAsync(subscriber, cancellationToken);
        return snapshot?.Meters.FirstOrDefault(m =>
            string.Equals(m.MeterCode, meterCode, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<List<MeterEntitlement>> BuildMetersAsync(
        Guid tenantId,
        SubscriberRef subscriber,
        Guid planVersionId,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        var entitlements = await _dbContext.PlanEntitlements.AsNoTracking()
            .Where(e => e.PlanVersionId == planVersionId)
            .ToListAsync(cancellationToken);

        var codes = entitlements.Select(e => e.MeterCode).Distinct().ToList();

        var meters = await _dbContext.Meters.AsNoTracking()
            .Where(m => m.TenantId == tenantId && codes.Contains(m.Code))
            .ToDictionaryAsync(m => m.Code, cancellationToken);

        // Grants are keyed by SUBSCRIBER, so purchased units bought under an earlier subscription
        // are counted here too — which is the point of keying them that way.
        var grants = await _dbContext.EntitlementGrants.AsNoTracking()
            .Where(g => g.TenantId == tenantId
                        && g.SubscriberKind == subscriber.Kind
                        && g.SubscriberId == subscriber.Id
                        && g.Status == GrantStatuses.Open
                        && (g.ExpiresAt == null || g.ExpiresAt > now))
            .ToListAsync(cancellationToken);

        var result = new List<MeterEntitlement>();

        foreach (var entitlement in entitlements)
        {
            var meter = meters.GetValueOrDefault(entitlement.MeterCode);
            var kind = meter?.Kind ?? MeterKinds.Counter;
            var forMeter = grants.Where(g => g.MeterCode == entitlement.MeterCode).ToList();

            var allowance = kind == MeterKinds.Counter ? forMeter.Sum(g => g.Allowance) : entitlement.Allowance;
            var consumed = kind == MeterKinds.Counter ? forMeter.Sum(g => g.Consumed) : 0m;
            var held = kind == MeterKinds.Counter ? forMeter.Sum(g => g.Held) : 0m;

            result.Add(new MeterEntitlement(
                entitlement.MeterCode,
                kind,
                meter?.Unit,
                allowance,
                consumed,
                held,
                Math.Max(0, allowance - consumed - held),
                entitlement.ResetPolicy,
                forMeter.Where(g => g.ExpiresAt.HasValue).Select(g => g.ExpiresAt!.Value).DefaultIfEmpty().Min() is var e && e == default ? null : e));
        }

        return result;
    }
}
