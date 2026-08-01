using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Persistence;
using Aonik.SharedKernel.Abstractions.Billing;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Abstractions.Payments;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Subscriptions.Entities.Catalogue;
using Aonik.Subscriptions.Entities.Subscriptions;
using Aonik.Subscriptions.Persistence;
using Aonik.Subscriptions.Services.Usage;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Subscriptions.Services.Subscriptions;

/// <summary>
/// Spec 087 §12.1 — renewing a paid subscription, and retrying one that failed.
///
/// The job that calls this arrives in P7; the flow lives here so it is testable without a
/// scheduler. Every step is idempotent per side effect, not merely per period: the anchor protects
/// the order and nothing else, so a job that dies between raising an invoice and recording its id
/// would otherwise raise a second one.
/// </summary>
internal sealed class SubscriptionRenewalService
{
    /// <summary>Attempts before a subscription is given up on. Beyond this, retrying is not going to help.</summary>
    private const int MaxAttempts = 4;

    private readonly SubscriptionsDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly EntitlementMaterialiser _materialiser;
    private readonly IOrderService _orders;
    private readonly IInvoiceWriter _invoices;
    private readonly IRecurringPaymentInitiator _payments;
    private readonly IClock _clock;

    public SubscriptionRenewalService(
        SubscriptionsDbContext dbContext,
        ITenantProvider tenantProvider,
        EntitlementMaterialiser materialiser,
        IOrderService orders,
        IInvoiceWriter invoices,
        IRecurringPaymentInitiator payments,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _materialiser = materialiser;
        _orders = orders;
        _invoices = invoices;
        _payments = payments;
        _clock = clock;
    }

    /// <summary>
    /// Tenants holding at least one subscription this service would act on.
    /// </summary>
    /// <remarks>
    /// The scheduled jobs run with <b>no ambient tenant</b>, and every other method here starts with
    /// <c>GetCurrentTenantId()</c> — which throws under <c>HttpContextTenantProvider</c>. So the job
    /// cannot simply call <c>FindDueAsync</c>; it has to be told which tenants to become first.
    /// Derived from the data rather than the tenant table, so an idle tenant costs nothing and a
    /// tenant whose rows arrived out of band is not missed.
    /// </remarks>
    public async Task<IReadOnlyList<Guid>> FindTenantsWithWorkAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        var fromSubscriptions = await _dbContext.Subscriptions
            .AsNoTracking()
            .AcrossTenants()
            .Where(s => !s.IsDeleted
                        && (s.Status == SubscriptionStatuses.Active || s.Status == SubscriptionStatuses.PastDue)
                        && s.CurrentPeriodEnd <= now)
            .Select(s => s.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var fromPeriods = await _dbContext.SubscriptionPeriods
            .AsNoTracking()
            .AcrossTenants()
            .Where(p => !p.IsDeleted && p.Status != SubscriptionPeriodStatuses.Settled)
            .Select(p => p.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return fromSubscriptions.Concat(fromPeriods).Distinct().ToList();
    }

    /// <summary>Subscriptions whose period has ended and which are still meant to renew.</summary>
    public async Task<IReadOnlyList<Guid>> FindDueAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;

        // Two ways to be due: the period has ended, or a period exists that was never paid for.
        // The second is not an edge case — it is how EVERY paid subscription starts, because
        // SubscribeAsync deliberately grants nothing until money actually arrives.
        var unsettled = _dbContext.SubscriptionPeriods.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Status == SubscriptionPeriodStatuses.Pending)
            .Select(p => p.SubscriptionId);

        return await _dbContext.Subscriptions.AsNoTracking()
            .Where(s => s.TenantId == tenantId
                        && s.Status == SubscriptionStatuses.Active
                        && (s.CurrentPeriodEnd <= now || unsettled.Contains(s.Id)))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<RenewalOutcome> RenewAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var subscription = await _dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Subscription '{subscriptionId}' was not found.");

        // Checked FIRST, before a period exists. Selecting on status alone would leave a cancelled
        // subscription matching the renewal query forever and billing the subscriber after they
        // explicitly cancelled.
        if (subscription.CancelAtPeriodEnd)
        {
            subscription.Status = SubscriptionStatuses.Cancelled;
            subscription.EndedAt = subscription.CurrentPeriodEnd;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return RenewalOutcome.Closed;
        }

        var hasOpenPeriod = await _dbContext.SubscriptionPeriods.AsNoTracking()
            .AnyAsync(p => p.SubscriptionId == subscription.Id && p.Status != SubscriptionPeriodStatuses.Settled,
                cancellationToken);

        // Idempotent at the top: a job that runs again before the next boundary must not mint a
        // period nobody owes anything for.
        if (!hasOpenPeriod && subscription.CurrentPeriodEnd > _clock.UtcNow)
            return RenewalOutcome.NothingToDo;

        var version = await ResolveVersionForNextPeriodAsync(subscription, cancellationToken);
        var period = await EnsurePeriodAsync(subscription, cancellationToken);

        // Zero-total periods never touch payment: LedgerPostingService rejects non-positive
        // amounts, so a £0 renewal routed through capture could never settle.
        if (version.Price <= 0)
        {
            await SettleAsync(subscription, period, version, cancellationToken);
            return RenewalOutcome.Settled;
        }

        if (subscription.PaymentMandateId is not { } mandateId)
        {
            await MarkPastDueAsync(subscription, period, "No payment mandate", cancellationToken);
            return RenewalOutcome.PastDue;
        }

        var order = await EnsureOrderAsync(subscription, period, version, cancellationToken);
        await EnsureInvoiceAsync(subscription, period, version, order.Id, cancellationToken);

        try
        {
            await EnsurePaymentIntentAsync(subscription, period, version, order.Id, mandateId, cancellationToken);
        }
        catch (MandateUnavailableException)
        {
            // Distinct from a soft decline on purpose. Retrying cannot restore a withdrawn
            // authorisation, so this stops rather than looping, and the customer is surfaced for
            // re-authorisation.
            await MarkPastDueAsync(subscription, period, "Payment mandate unavailable", cancellationToken, retryable: false);
            return RenewalOutcome.NeedsReauthorisation;
        }

        await SettleAsync(subscription, period, version, cancellationToken);
        await CompleteOrderAsync(order.Id, period.Id, cancellationToken);

        return RenewalOutcome.Settled;
    }

    /// <summary>Periods in <c>past_due</c> whose retry is due (Spec 087 §12.5).</summary>
    public async Task<IReadOnlyList<Guid>> FindRetryableAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;

        return await _dbContext.SubscriptionPeriods.AsNoTracking()
            .Where(p => p.TenantId == tenantId
                        && p.Status == SubscriptionPeriodStatuses.Failed
                        && p.NextAttemptAt != null
                        && p.NextAttemptAt <= now)
            .Select(p => p.SubscriptionId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<RenewalOutcome> RetryAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var subscription = await _dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Subscription '{subscriptionId}' was not found.");

        var period = await _dbContext.SubscriptionPeriods
            .Where(p => p.SubscriptionId == subscriptionId && p.Status == SubscriptionPeriodStatuses.Failed)
            .OrderByDescending(p => p.Sequence)
            .FirstOrDefaultAsync(cancellationToken);

        if (period is null)
            return RenewalOutcome.NothingToDo;

        if (period.AttemptCount >= MaxAttempts)
        {
            // Bounded. Past this point retrying is not going to help, and leaving it in past_due
            // forever hides the subscription from every selector.
            subscription.Status = SubscriptionStatuses.Expired;
            subscription.EndedAt = _clock.UtcNow;
            period.NextAttemptAt = null;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return RenewalOutcome.Expired;
        }

        var version = await _dbContext.PlanVersions.AsNoTracking()
            .FirstAsync(v => v.Id == subscription.PlanVersionId, cancellationToken);

        if (subscription.PaymentMandateId is not { } mandateId)
        {
            await MarkPastDueAsync(subscription, period, "No payment mandate", cancellationToken);
            return RenewalOutcome.PastDue;
        }

        try
        {
            // A FRESH intent per attempt. PaymentService treats Failed as terminal — capture
            // requires Authorized — so reusing the failed one would strand every hard decline
            // exactly where this is supposed to help.
            var attempt = period.AttemptCount + 1;
            var reference = await _payments.CreateIntentForMandateAsync(
                mandateId, period.OrderId!.Value, version.Price, version.Currency,
                $"sub:{subscription.Id}:period:{period.Sequence}:attempt:{attempt}", cancellationToken);

            period.PaymentIntentId = reference.PaymentIntentId;
            period.AttemptCount = attempt;
        }
        catch (MandateUnavailableException)
        {
            await MarkPastDueAsync(subscription, period, "Payment mandate unavailable", cancellationToken, retryable: false);
            return RenewalOutcome.NeedsReauthorisation;
        }

        await SettleAsync(subscription, period, version, cancellationToken);
        await CompleteOrderAsync(period.OrderId!.Value, period.Id, cancellationToken);

        return RenewalOutcome.Settled;
    }

    // ---- steps -------------------------------------------------------------------------------

    private async Task<PlanVersion> ResolveVersionForNextPeriodAsync(
        Subscription subscription,
        CancellationToken cancellationToken)
    {
        // Read, never applied here. Applying a pending upgrade before payment would hand over its
        // flags and higher ceilings unpaid, and lose the version the subscriber is entitled to.
        var versionId = subscription.PendingPlanVersionId ?? subscription.PlanVersionId;
        return await _dbContext.PlanVersions.AsNoTracking().FirstAsync(v => v.Id == versionId, cancellationToken);
    }

    private async Task<SubscriptionPeriod> EnsurePeriodAsync(Subscription subscription, CancellationToken cancellationToken)
    {
        // The anchor: unique on (SubscriptionId, Sequence). A job that runs twice reuses the open
        // period rather than minting a second, which is what stops it double-billing.
        var existing = await _dbContext.SubscriptionPeriods
            .Where(p => p.SubscriptionId == subscription.Id && p.Status != SubscriptionPeriodStatuses.Settled)
            .OrderBy(p => p.Sequence)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
            return existing;

        var nextSequence = await _dbContext.SubscriptionPeriods
            .Where(p => p.SubscriptionId == subscription.Id)
            .Select(p => (int?)p.Sequence)
            .MaxAsync(cancellationToken) ?? 0;

        var period = new SubscriptionPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = subscription.TenantId,
            SubscriptionId = subscription.Id,
            Sequence = nextSequence + 1,
            StartsAt = subscription.CurrentPeriodEnd,
            // The PLAN's interval, not a month. Hard-coding AddMonths here charged an annual
            // subscriber the annual price every month after their first year, and reset their
            // period entitlements monthly with it.
            EndsAt = BillingInterval.Add(subscription.CurrentPeriodEnd, await ResolveIntervalAsync(subscription, cancellationToken)),
            Status = SubscriptionPeriodStatuses.Pending
        };

        _dbContext.SubscriptionPeriods.Add(period);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return period;
    }

    /// <summary>The billing interval of the plan this subscription is pinned to.</summary>
    private async Task<string> ResolveIntervalAsync(Subscription subscription, CancellationToken cancellationToken)
    {
        var interval = await _dbContext.PlanVersions
            .AsNoTracking()
            .Where(v => v.Id == subscription.PlanVersionId)
            .Join(_dbContext.Plans.AsNoTracking(), v => v.PlanId, p => p.Id, (_, p) => p.BillingInterval)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(interval) ? BillingIntervals.Month : interval;
    }

    private async Task<OrderDto> EnsureOrderAsync(
        Subscription subscription,
        SubscriptionPeriod period,
        PlanVersion version,
        CancellationToken cancellationToken)
    {
        var key = $"sub:{subscription.Id}:period:{period.Sequence}";

        if (period.OrderId is { } existingId)
        {
            var existing = await _orders.GetAsync(existingId, cancellationToken);
            if (existing is not null)
                return existing;
        }

        var order = await _orders.FindByIdempotencyKeyAsync(key, cancellationToken)
            ?? await _orders.CreateAsync(
                new CreateOrderCommand(
                    OrderTypeCodes.SubscriptionRenewal,
                    PayerPartyId: subscription.SubscriberKind == SubscriberKinds.Party ? subscription.SubscriberId : null,
                    CurrencyIn: version.Currency,
                    Items: [new OrderItemCommand("PlanPeriod", 0, version.Price, version.Currency, Quantity: 1, UnitPrice: version.Price)],
                    IdempotencyKey: key),
                cancellationToken);

        period.OrderId = order.Id;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return order;
    }

    private async Task EnsureInvoiceAsync(
        Subscription subscription,
        SubscriptionPeriod period,
        PlanVersion version,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        // Persisted the moment the writer returns. Without this a crash here means a retry raises
        // a second invoice — the anchor protects the order, not the side effects.
        if (period.InvoiceId is not null)
            return;

        var reference = await _invoices.CreateForOrderAsync(
            new CreateInvoiceForOrderCommand(
                OrderId: orderId,
                CustomerId: subscription.SubscriberId,
                Currency: version.Currency,
                Lines: [new InvoiceLineSpec($"Subscription period {period.Sequence}", 1, version.Price)],
                IdempotencyKey: $"sub-invoice:{subscription.Id}:{period.Sequence}"),
            cancellationToken);

        period.InvoiceId = reference.InvoiceId;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsurePaymentIntentAsync(
        Subscription subscription,
        SubscriptionPeriod period,
        PlanVersion version,
        Guid orderId,
        Guid mandateId,
        CancellationToken cancellationToken)
    {
        if (period.PaymentIntentId is not null)
            return;

        var attempt = period.AttemptCount + 1;

        var reference = await _payments.CreateIntentForMandateAsync(
            mandateId, orderId, version.Price, version.Currency,
            $"sub:{subscription.Id}:period:{period.Sequence}:attempt:{attempt}", cancellationToken);

        period.PaymentIntentId = reference.PaymentIntentId;
        period.AttemptCount = attempt;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SettleAsync(
        Subscription subscription,
        SubscriptionPeriod period,
        PlanVersion version,
        CancellationToken cancellationToken)
    {
        // ONLY now is the pending plan applied — an unpaid upgrade must confer nothing.
        if (subscription.PendingPlanVersionId is { } pending)
        {
            subscription.PlanVersionId = pending;
            subscription.PendingPlanVersionId = null;
            subscription.PendingEffectiveAt = null;
        }

        await _materialiser.MaterialiseForPeriodAsync(subscription, period, version.Id, cancellationToken);

        subscription.CurrentPeriodStart = period.StartsAt;
        subscription.CurrentPeriodEnd = period.EndsAt;
        subscription.Status = SubscriptionStatuses.Active;

        period.Status = SubscriptionPeriodStatuses.Settled;
        period.NextAttemptAt = null;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task CompleteOrderAsync(Guid orderId, Guid periodId, CancellationToken cancellationToken)
    {
        var order = await _orders.GetAsync(orderId, cancellationToken);

        // Orders are created Draft. Leaving one there after the period was actually served would
        // leave the canonical record of the transaction contradicting what happened.
        if (order is null || OrderStatusCodes.IsTerminal(order.Status))
            return;

        // Spec 087 §12 — what actually fulfilled this order. The three original references are all
        // money-movement records, so until OrderFulfilmentLink carried a period a subscription
        // renewal completed its order with no fulfilment trace at all.
        await _orders.LinkFulfilmentAsync(
            orderId, new OrderFulfilmentLink(SubscriptionPeriodId: periodId), cancellationToken);

        await _orders.TransitionAsync(orderId, OrderStatusCodes.Complete, "Subscription period settled",
            expectedFromStatus: order.Status, cancellationToken);
    }

    private async Task MarkPastDueAsync(
        Subscription subscription,
        SubscriptionPeriod period,
        string reason,
        CancellationToken cancellationToken,
        bool retryable = true)
    {
        subscription.Status = SubscriptionStatuses.PastDue;

        period.Status = SubscriptionPeriodStatuses.Failed;
        period.AttemptCount += 1;
        // Null means "do not retry": a withdrawn authorisation needs the customer, not the job.
        period.NextAttemptAt = retryable ? _clock.UtcNow.AddDays(BackoffDays(period.AttemptCount)) : null;

        // Grants are NOT materialised. An unpaid period confers no allowance — that is the whole
        // reason materialisation sits in settlement rather than in creating the period.
        await _dbContext.SaveChangesAsync(cancellationToken);

        _ = reason;
    }

    private static int BackoffDays(int attemptCount) => attemptCount switch
    {
        <= 1 => 1,
        2 => 3,
        _ => 7
    };
}

/// <summary>What a renewal attempt did.</summary>
internal enum RenewalOutcome
{
    NothingToDo,
    Settled,
    PastDue,

    /// <summary>The mandate is gone. Not retryable — the customer must re-authorise.</summary>
    NeedsReauthorisation,

    /// <summary>Cancelled at the period boundary, as the subscriber asked.</summary>
    Closed,

    /// <summary>Given up on after the bounded retry count.</summary>
    Expired
}
