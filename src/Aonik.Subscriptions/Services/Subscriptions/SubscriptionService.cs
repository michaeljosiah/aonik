using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Subscriptions.Entities.Catalogue;
using Aonik.Subscriptions.Entities.Subscriptions;
using Aonik.Subscriptions.Persistence;
using Aonik.Subscriptions.Services.Usage;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Subscriptions.Services.Subscriptions;

/// <summary>
/// Spec 087 §7, §12 — the subscription lifecycle.
///
/// P3 covers everything that does not move money: subscribing, cancelling, resuming, and the
/// <b>zero-total settlement path</b> that makes the free tier work. Paid renewal, dunning and
/// entitlement purchases arrive in P5/P6 once Spec 088's Finance contracts are in place.
/// </summary>
internal sealed class SubscriptionService : ISubscriptionService
{
    private readonly SubscriptionsDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly SubscriberAuthorization _authorization;
    private readonly EntitlementMaterialiser _materialiser;
    private readonly IClock _clock;

    public SubscriptionService(
        SubscriptionsDbContext dbContext,
        ITenantProvider tenantProvider,
        SubscriberAuthorization authorization,
        EntitlementMaterialiser materialiser,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _authorization = authorization;
        _materialiser = materialiser;
        _clock = clock;
    }

    public async Task<SubscriptionDto> SubscribeAsync(
        SubscriberRef subscriber,
        string planCode,
        Guid? paymentMandateId = null,
        CancellationToken cancellationToken = default)
    {
        await _authorization.EnsureCanManageBillingForAsync(subscriber, cancellationToken);

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var code = planCode.Trim().ToLowerInvariant();

        var plan = await _dbContext.Plans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Code == code, cancellationToken)
            ?? throw new NotFoundException($"Plan '{planCode}' was not found.");

        if (plan.Status == PlanStatuses.Retired)
            throw new InvalidStateException($"Plan '{plan.Code}' is retired and cannot be subscribed to.");

        var version = await CurrentVersionAsync(plan.Id, cancellationToken)
            ?? throw new InvalidStateException($"Plan '{plan.Code}' has no published version to subscribe to.");

        // Service-level check for a clean error; the filtered unique index is what actually holds
        // under a concurrent Subscribe, and a subscriber renewed twice is charged twice.
        var occupied = await _dbContext.Subscriptions.AsNoTracking()
            .AnyAsync(s => s.TenantId == tenantId
                           && s.SubscriberKind == subscriber.Kind
                           && s.SubscriberId == subscriber.Id
                           && SubscriptionStatuses.OccupiesActiveSlotQueryable.Contains(s.Status),
                cancellationToken);

        if (occupied)
            throw new InvalidStateException("This subscriber already holds an active subscription.");

        if (version.Price > 0 && paymentMandateId is null)
        {
            // A priced plan with no way to charge it would renew into past_due on day one.
            throw new InvalidStateException(
                $"Plan '{plan.Code}' is priced at {version.Price} {version.Currency} and requires a payment mandate.");
        }

        var now = _clock.UtcNow;
        var periodEnd = AddInterval(now, plan.BillingInterval);

        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SubscriberKind = subscriber.Kind,
            SubscriberId = subscriber.Id,
            PlanVersionId = version.Id,
            Status = SubscriptionStatuses.Active,
            CurrentPeriodStart = now,
            CurrentPeriodEnd = periodEnd,
            PaymentMandateId = paymentMandateId,
            StartedAt = now
        };

        var period = new SubscriptionPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SubscriptionId = subscription.Id,
            Sequence = 1,
            StartsAt = now,
            EndsAt = periodEnd,
            Status = SubscriptionPeriodStatuses.Pending
        };

        _dbContext.Subscriptions.Add(subscription);
        _dbContext.SubscriptionPeriods.Add(period);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await SettleIfFreeAsync(subscription, period, version, cancellationToken);

        return await MapAsync(subscription, cancellationToken);
    }

    public async Task<SubscriptionDto> ChangePlanAsync(
        Guid subscriptionId,
        string planCode,
        CancellationToken cancellationToken = default)
    {
        var subscription = await LoadAuthorisedAsync(subscriptionId, cancellationToken);
        var tenantId = subscription.TenantId;
        var code = planCode.Trim().ToLowerInvariant();

        var plan = await _dbContext.Plans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Code == code, cancellationToken)
            ?? throw new NotFoundException($"Plan '{planCode}' was not found.");

        var version = await CurrentVersionAsync(plan.Id, cancellationToken)
            ?? throw new InvalidStateException($"Plan '{plan.Code}' has no published version.");

        // Recorded, not applied. Applying now would hand over the new plan's capability before it
        // is paid for, and would lose the version the subscriber is actually entitled to.
        subscription.PendingPlanVersionId = version.Id;
        subscription.PendingEffectiveAt = subscription.CurrentPeriodEnd;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await MapAsync(subscription, cancellationToken);
    }

    public async Task<SubscriptionDto> RefreshFreeCapacityAsync(Guid subscriptionId, Guid expectedVersionId, Guid targetVersionId, CancellationToken cancellationToken = default)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = _dbContext.Database.IsRelational()
                ? await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
                : null;
            var subscription = await LoadAuthorisedAsync(subscriptionId, cancellationToken);
            // A retry after an uncertain commit must inspect persisted state, not EF's prior values.
            await _dbContext.Entry(subscription).ReloadAsync(cancellationToken);
            if (subscription.Status != SubscriptionStatuses.Active || subscription.PendingPlanVersionId is not null || subscription.CancelAtPeriodEnd)
                throw new InvalidStateException("Capacity can only be refreshed on an active subscription without a pending change.");
            if (subscription.PlanVersionId == targetVersionId) return await MapAsync(subscription, cancellationToken);
            if (subscription.PlanVersionId != expectedVersionId) throw new InvalidStateException("The subscription changed. Read it again before refreshing capacity.");
            var source = await _dbContext.PlanVersions.AsNoTracking().SingleAsync(v => v.Id == expectedVersionId && v.TenantId == subscription.TenantId, cancellationToken);
            var target = await _dbContext.PlanVersions.AsNoTracking().SingleOrDefaultAsync(v => v.Id == targetVersionId && v.TenantId == subscription.TenantId, cancellationToken)
                ?? throw new NotFoundException("No such plan version.");
            var plan = await _dbContext.Plans.AsNoTracking().SingleAsync(p => p.Id == source.PlanId, cancellationToken);
            if (source.Price != 0 || target.Price != 0 || target.Currency != source.Currency || target.PlanId != source.PlanId
                || plan.BillingInterval != BillingIntervals.None || target.Status != PlanVersionStatuses.Published || target.EffectiveFrom > _clock.UtcNow)
                throw new InvalidStateException("Only the same free, non-renewing plan may receive a capacity refresh.");
            var oldItems = await _dbContext.PlanEntitlements.AsNoTracking().Where(e => e.PlanVersionId == source.Id).ToListAsync(cancellationToken);
            var newItems = await _dbContext.PlanEntitlements.AsNoTracking().Where(e => e.PlanVersionId == target.Id).ToListAsync(cancellationToken);
            var kinds = await _dbContext.Meters.AsNoTracking().Where(m => m.TenantId == subscription.TenantId).ToDictionaryAsync(m => m.Code, m => m.Kind, cancellationToken);
            foreach (var oldItem in oldItems)
            {
                var newItem = newItems.SingleOrDefault(e => e.MeterCode == oldItem.MeterCode);
                if (newItem is null || newItem.ResetPolicy != oldItem.ResetPolicy ||
                    (kinds[oldItem.MeterCode] == MeterKinds.Ceiling ? newItem.Allowance < oldItem.Allowance : newItem.Allowance != oldItem.Allowance))
                    throw new InvalidStateException("A capacity refresh cannot remove entitlements or change counter grants.");
            }
            if (newItems.Any(e => oldItems.All(old => old.MeterCode != e.MeterCode) && kinds[e.MeterCode] != MeterKinds.Ceiling))
                throw new InvalidStateException("A capacity refresh can only add ceiling entitlements.");
            subscription.PlanVersionId = target.Id;
            await _dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return await MapAsync(subscription, cancellationToken);
        });
    }

    public async Task<SubscriptionDto> CancelAsync(
        Guid subscriptionId,
        bool atPeriodEnd = true,
        CancellationToken cancellationToken = default)
    {
        var subscription = await LoadAuthorisedAsync(subscriptionId, cancellationToken);

        if (atPeriodEnd)
        {
            // The subscriber keeps what they have paid for; the renewal job closes it at the
            // boundary rather than billing again.
            subscription.CancelAtPeriodEnd = true;
        }
        else
        {
            subscription.Status = SubscriptionStatuses.Cancelled;
            subscription.EndedAt = _clock.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await MapAsync(subscription, cancellationToken);
    }

    public async Task<SubscriptionDto> ResumeAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        var subscription = await LoadAuthorisedAsync(subscriptionId, cancellationToken);

        if (SubscriptionStatuses.IsTerminal(subscription.Status))
        {
            // Once closed it is closed — resuming would resurrect a subscription whose period has
            // already lapsed. Subscribing again is the correct path.
            throw new InvalidStateException(
                $"Subscription is '{subscription.Status}' and cannot be resumed. Subscribe again instead.");
        }

        subscription.CancelAtPeriodEnd = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await MapAsync(subscription, cancellationToken);
    }

    public async Task<SubscriptionDto> SetPaymentMandateAsync(
        Guid subscriptionId,
        Guid paymentMandateId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await LoadAuthorisedAsync(subscriptionId, cancellationToken);
        subscription.PaymentMandateId = paymentMandateId;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await MapAsync(subscription, cancellationToken);
    }

    public async Task<SubscriptionDto?> GetAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var subscription = await _dbContext.Subscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.TenantId == tenantId, cancellationToken);

        if (subscription is null)
            return null;

        await _authorization.EnsureCanManageBillingForAsync(
            new SubscriberRef(subscription.SubscriberKind, subscription.SubscriberId), cancellationToken);

        return await MapAsync(subscription, cancellationToken);
    }

    public async Task<SubscriptionDto?> GetForSubscriberAsync(
        SubscriberRef subscriber,
        CancellationToken cancellationToken = default)
    {
        await _authorization.EnsureCanManageBillingForAsync(subscriber, cancellationToken);

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var subscription = await _dbContext.Subscriptions.AsNoTracking()
            .Where(s => s.TenantId == tenantId
                        && s.SubscriberKind == subscriber.Kind
                        && s.SubscriberId == subscriber.Id
                        && SubscriptionStatuses.OccupiesActiveSlotQueryable.Contains(s.Status))
            .FirstOrDefaultAsync(cancellationToken);

        return subscription is null ? null : await MapAsync(subscription, cancellationToken);
    }

    // ---- internals ---------------------------------------------------------------------------

    /// <summary>
    /// Spec 087 §12.2 — a zero-total period settles directly.
    ///
    /// It cannot go through invoice → intent → capture: <c>LedgerPostingService</c> rejects
    /// non-positive amounts outright, so a £0 plan routed that way would never settle and never
    /// receive its allowance. Nothing was earned and nothing moved, so there is nothing to post.
    /// </summary>
    private async Task SettleIfFreeAsync(
        Subscription subscription,
        SubscriptionPeriod period,
        PlanVersion version,
        CancellationToken cancellationToken)
    {
        if (version.Price > 0)
            return;

        await _materialiser.MaterialiseForPeriodAsync(subscription, period, version.Id, cancellationToken);

        period.Status = SubscriptionPeriodStatuses.Settled;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Subscription> LoadAuthorisedAsync(Guid subscriptionId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var subscription = await _dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Subscription '{subscriptionId}' was not found.");

        await _authorization.EnsureCanManageBillingForAsync(
            new SubscriberRef(subscription.SubscriberKind, subscription.SubscriberId), cancellationToken);

        return subscription;
    }

    private async Task<PlanVersion?> CurrentVersionAsync(Guid planId, CancellationToken cancellationToken)
        => await _dbContext.PlanVersions.AsNoTracking()
            .Where(v => v.PlanId == planId && v.Status == PlanVersionStatuses.Published)
            .OrderByDescending(v => v.Version)
            .FirstOrDefaultAsync(cancellationToken);

    private static DateTime AddInterval(DateTime from, string billingInterval)
        => BillingInterval.Add(from, billingInterval);

    /// <summary>The DTO names the plan by code, not only by version id: the version is pinned, the code is what a product and an operator talk about.</summary>
    private async Task<SubscriptionDto> MapAsync(Subscription s, CancellationToken cancellationToken)
    {
        var versionIds = new[] { s.PlanVersionId, s.PendingPlanVersionId ?? Guid.Empty };
        var codes = await _dbContext.PlanVersions.AsNoTracking()
            .Where(v => versionIds.Contains(v.Id))
            .Join(_dbContext.Plans.AsNoTracking(), v => v.PlanId, plan => plan.Id, (v, plan) => new { v.Id, plan.Code })
            .ToDictionaryAsync(x => x.Id, x => x.Code, cancellationToken);

        return new SubscriptionDto(
            s.Id, new SubscriberRef(s.SubscriberKind, s.SubscriberId),
            codes.GetValueOrDefault(s.PlanVersionId, string.Empty), s.PlanVersionId,
            s.PendingPlanVersionId is { } pending ? codes.GetValueOrDefault(pending) : null, s.PendingEffectiveAt,
            s.Status, s.CurrentPeriodStart, s.CurrentPeriodEnd,
            s.CancelAtPeriodEnd, s.PaymentMandateId, s.StartedAt, s.EndedAt);
    }
}
