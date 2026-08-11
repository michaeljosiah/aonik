using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Subscriptions.Entities.Subscriptions;
using Aonik.Subscriptions.Entities.Usage;
using Aonik.Subscriptions.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Subscriptions.Services.Usage;

/// <summary>
/// Turns a settled period into the grants it confers (Spec 087 §8).
///
/// Only ever called when a period <b>settles</b>: an unpaid period confers no allowance, which is
/// the whole reason grant materialisation is not part of creating the period.
/// </summary>
internal sealed class EntitlementMaterialiser
{
    private readonly SubscriptionsDbContext _dbContext;
    private readonly IClock _clock;

    public EntitlementMaterialiser(SubscriptionsDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task MaterialiseForPeriodAsync(
        Subscription subscription,
        SubscriptionPeriod period,
        Guid planVersionId,
        CancellationToken cancellationToken = default)
    {
        // Payment completion is at-least-once, so a retried or concurrently-handled event must not
        // double the allowance. The unique index on (PeriodId, MeterCode, Source) is the authority;
        // this check turns the ordinary retry into a no-op rather than a constraint violation.
        var alreadyMaterialised = await _dbContext.EntitlementGrants.AsNoTracking()
            .AnyAsync(g => g.PeriodId == period.Id && g.Source == GrantSources.Plan, cancellationToken);

        if (alreadyMaterialised)
            return;

        var entitlements = await _dbContext.PlanEntitlements.AsNoTracking()
            .Where(e => e.PlanVersionId == planVersionId)
            .ToListAsync(cancellationToken);

        if (entitlements.Count == 0)
            return;

        var meters = await MeterKindsByCodeAsync(
            subscription.TenantId,
            entitlements.Select(e => e.MeterCode).Distinct().ToList(),
            cancellationToken);

        foreach (var entitlement in entitlements)
        {
            // Only counters are drawn down, so only counters become grants. A ceiling is a
            // maximum held and a flag is a capability — neither is an allowance to spend, and
            // giving them grants would make "remaining" meaningless for both.
            if (!meters.TryGetValue(entitlement.MeterCode, out var kind) || kind != MeterKinds.Counter)
                continue;

            _dbContext.EntitlementGrants.Add(new EntitlementGrant
            {
                Id = Guid.NewGuid(),
                TenantId = subscription.TenantId,
                SubscriberKind = subscription.SubscriberKind,
                SubscriberId = subscription.SubscriberId,
                SubscriptionId = subscription.Id,
                PeriodId = period.Id,
                MeterCode = entitlement.MeterCode,
                Source = GrantSources.Plan,
                Allowance = entitlement.Allowance,
                Consumed = 0,
                Held = 0,
                // Derived from the RESET POLICY, not from the source: a `never` entitlement
                // accumulates across renewals instead of being discarded each period end.
                ExpiresAt = entitlement.ResetPolicy == ResetPolicies.Never ? null : period.EndsAt,
                Status = GrantStatuses.Open
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<string, string>> MeterKindsByCodeAsync(
        Guid tenantId,
        List<string> codes,
        CancellationToken cancellationToken)
        => await _dbContext.Meters.AsNoTracking()
            .Where(m => m.TenantId == tenantId && codes.Contains(m.Code))
            .ToDictionaryAsync(m => m.Code, m => m.Kind, cancellationToken);
}
