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
        {
            // No subscription is not "no entitlement". Purchased grants outlive subscriptions by
            // design, so a subscriber holding one still has something to report — and returning
            // null here is what made the documented pre-check refuse paid-for work.
            var purchased = await BuildPurchasedOnlyMetersAsync(tenantId, subscriber, cancellationToken);

            return purchased.Count == 0
                ? null
                : new EntitlementSnapshot(subscriber, null, string.Empty, string.Empty, null, null, null, null, purchased);
        }

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

    /// <summary>
    /// Meters assembled from the subscriber's open grants alone, with no plan to read them against.
    /// </summary>
    /// <remarks>
    /// Only counter meters appear. A ceiling or a flag is a statement about what a <em>plan</em>
    /// permits, so without one there is nothing to report; a counter is a balance the subscriber
    /// owns outright.
    /// </remarks>
    private async Task<List<MeterEntitlement>> BuildPurchasedOnlyMetersAsync(
        Guid tenantId,
        SubscriberRef subscriber,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        var grants = await _dbContext.EntitlementGrants.AsNoTracking()
            .Where(g => g.TenantId == tenantId
                        && g.SubscriberKind == subscriber.Kind
                        && g.SubscriberId == subscriber.Id
                        && g.Status == GrantStatuses.Open
                        && (g.ExpiresAt == null || g.ExpiresAt > now))
            .ToListAsync(cancellationToken);

        if (grants.Count == 0)
            return [];

        var codes = grants.Select(g => g.MeterCode).Distinct().ToList();

        var meters = await _dbContext.Meters.AsNoTracking()
            .Where(m => m.TenantId == tenantId && codes.Contains(m.Code))
            .ToDictionaryAsync(m => m.Code, cancellationToken);

        var result = new List<MeterEntitlement>();

        foreach (var group in grants.GroupBy(g => g.MeterCode))
        {
            var meter = meters.GetValueOrDefault(group.Key);

            if (meter is not null && meter.Kind != MeterKinds.Counter)
                continue;

            var allowance = group.Sum(g => g.Allowance);
            var consumed = group.Sum(g => g.Consumed);
            var held = group.Sum(g => g.Held);

            var expiries = group.Where(g => g.ExpiresAt.HasValue).Select(g => g.ExpiresAt!.Value).ToList();

            result.Add(new MeterEntitlement(
                group.Key,
                MeterKinds.Counter,
                meter?.Unit,
                allowance,
                consumed,
                held,
                Math.Max(0, allowance - consumed - held),
                // Nothing resets these: a purchased balance accumulates and is drawn down, which is
                // exactly why it survives the subscription that may have accompanied it.
                ResetPolicies.Never,
                expiries.Count == 0 ? null : expiries.Min()));
        }

        return result;
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
