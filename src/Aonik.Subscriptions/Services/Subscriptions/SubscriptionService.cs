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
        await _authorization.EnsureCanActForAsync(subscriber, cancellationToken);

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
                           && SubscriptionStatuses.OccupiesActiveSlot.Contains(s.Status),
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

        return Map(subscription);
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
        return Map(subscription);
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
        return Map(subscription);
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
        return Map(subscription);
    }

    public async Task<SubscriptionDto> SetPaymentMandateAsync(
        Guid subscriptionId,
        Guid paymentMandateId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await LoadAuthorisedAsync(subscriptionId, cancellationToken);
        subscription.PaymentMandateId = paymentMandateId;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(subscription);
    }

    public async Task<SubscriptionDto?> GetAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var subscription = await _dbContext.Subscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.TenantId == tenantId, cancellationToken);

        if (subscription is null)
            return null;

        await _authorization.EnsureCanActForAsync(
            new SubscriberRef(subscription.SubscriberKind, subscription.SubscriberId), cancellationToken);

        return Map(subscription);
    }

    public async Task<SubscriptionDto?> GetForSubscriberAsync(
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

        return subscription is null ? null : Map(subscription);
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

        await _authorization.EnsureCanActForAsync(
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

    private static SubscriptionDto Map(Subscription s)
        => new(s.Id, new SubscriberRef(s.SubscriberKind, s.SubscriberId), string.Empty, s.PlanVersionId,
            null, s.PendingEffectiveAt, s.Status, s.CurrentPeriodStart, s.CurrentPeriodEnd,
            s.CancelAtPeriodEnd, s.PaymentMandateId, s.StartedAt, s.EndedAt);
}
