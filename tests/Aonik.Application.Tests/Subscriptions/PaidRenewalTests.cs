using Aonik.Subscriptions.Entities.Usage;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Billing;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Abstractions.Payments;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Subscriptions.Contracts.Models;
using Aonik.Subscriptions.Persistence;
using Aonik.Subscriptions.Services.Catalogue;
using Aonik.Subscriptions.Services.Subscriptions;
using Aonik.Subscriptions.Services.Usage;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Subscriptions;

/// <summary>
/// Spec 087 P6 acceptance: a paid period renews idempotently; a failed payment yields
/// <c>past_due</c>, retries with a <b>fresh</b> intent, and materialises no grants.
///
/// Every flow correction from the round-2 review is exercised here — the cancel check before
/// billing, per-side-effect idempotency, order completion, the pending plan applying only on
/// settlement, and a withdrawn mandate stopping rather than looping.
/// </summary>
public class PaidRenewalTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private sealed class TestTenantProvider : ITenantProvider
    {
        public Guid GetCurrentTenantId() => TenantId;
        public bool TryGetCurrentTenantId(out Guid tenantId) { tenantId = TenantId; return true; }
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class AllowTenantAuthorizer : ISubscriberAuthorizer
    {
        public IReadOnlyCollection<string> SupportedKinds => [SubscriberKinds.Tenant];
        public Task<bool> CanActForAsync(SubscriberRef s, CancellationToken ct = default) => Task.FromResult(true);
    }

    /// <summary>Records what the renewal asked the order spine to do.</summary>
    private sealed class FakeOrderService : IOrderService
    {
        private readonly Dictionary<Guid, OrderDto> _orders = [];
        private readonly Dictionary<string, Guid> _byKey = [];

        public List<(Guid OrderId, string ToStatus)> Transitions { get; } = [];
        public int CreateCalls { get; private set; }

        public Task<OrderDto> CreateAsync(CreateOrderCommand command, CancellationToken ct = default)
        {
            CreateCalls++;

            if (command.IdempotencyKey is { } key && _byKey.TryGetValue(key, out var existingId))
                return Task.FromResult(_orders[existingId]);

            var id = Guid.NewGuid();
            var dto = new OrderDto(
                id,
                TenantId,
                command.OrderType,
                OrderStatusCodes.Draft,
                command.PayerPartyId,
                command.AmountIn ?? command.Items.Sum(i => i.AmountIn),
                command.CurrencyIn,
                DateTime.UtcNow,
                command.Items.Select((i, ix) => new OrderItemDto(Guid.NewGuid(), i.ItemType, ix, "Pending",
                    i.AmountIn, i.CurrencyIn, i.ReceiverPartyId, i.Quantity, i.UnitPrice, i.ProductId, i.Sku,
                    i.DetailsJson ?? "{}")).ToList());

            _orders[id] = dto;
            if (command.IdempotencyKey is { } k) _byKey[k] = id;
            return Task.FromResult(dto);
        }

        public Task<OrderDto?> GetAsync(Guid orderId, CancellationToken ct = default)
            => Task.FromResult(_orders.GetValueOrDefault(orderId));

        public Task<OrderDto?> FindByIdempotencyKeyAsync(string key, CancellationToken ct = default)
            => Task.FromResult(_byKey.TryGetValue(key, out var id) ? _orders[id] : null);

        public Task<OrderDto> TransitionAsync(Guid orderId, string toStatus, string? reason = null,
            string? expectedFromStatus = null, CancellationToken ct = default)
        {
            Transitions.Add((orderId, toStatus));
            _orders[orderId] = _orders[orderId] with { Status = toStatus };
            return Task.FromResult(_orders[orderId]);
        }

        public Task<PagedResult<OrderSummary>> ListAsync(ListOrdersQuery q, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PagedResult<OrderDto>> ListWithItemsAsync(ListOrdersQuery q, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, PartyOrderAggregate>> GetPartyOrderAggregatesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) => throw new NotSupportedException();
        public Task LinkFundingAsync(Guid orderId, Guid paymentIntentId, CancellationToken ct = default) => Task.CompletedTask;
        public Task LinkFulfilmentAsync(Guid orderId, OrderFulfilmentLink link, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeInvoiceWriter : IInvoiceWriter
    {
        public int Calls { get; private set; }

        public Task<InvoiceRef> CreateForOrderAsync(CreateInvoiceForOrderCommand command, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new InvoiceRef(Guid.NewGuid(), "INV-1", command.Lines.Sum(l => l.Quantity * l.UnitPrice), command.Currency));
        }
    }

    private sealed class FakeRecurringInitiator : IRecurringPaymentInitiator
    {
        public List<string> Keys { get; } = [];
        public bool MandateGone { get; set; }

        public Task<PaymentIntentRef> CreateIntentForMandateAsync(Guid mandateId, Guid orderId, decimal amount,
            string currency, string idempotencyKey, CancellationToken ct = default)
        {
            if (MandateGone)
                throw new MandateUnavailableException(mandateId, "it has been revoked");

            Keys.Add(idempotencyKey);
            return Task.FromResult(new PaymentIntentRef(Guid.NewGuid(), "Pending"));
        }
    }

    private sealed class Harness
    {
        public SubscriptionsDbContext Db { get; }
        public TestClock Clock { get; } = new();
        public CatalogueService Catalogue { get; }
        public SubscriptionService Subscriptions { get; }
        public SubscriptionRenewalService Renewals { get; }
        public EntitlementReader Reader { get; }
        public FakeOrderService Orders { get; } = new();
        public FakeInvoiceWriter Invoices { get; } = new();
        public FakeRecurringInitiator Payments { get; } = new();

        public Harness()
        {
            Db = new SubscriptionsDbContext(
                new DbContextOptionsBuilder<SubscriptionsDbContext>()
                    .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}").Options,
                new TestTenantProvider());

            var tenant = new TestTenantProvider();
            var auth = new SubscriberAuthorization([new AllowTenantAuthorizer()]);
            var materialiser = new EntitlementMaterialiser(Db, Clock);

            Catalogue = new CatalogueService(Db, tenant, Clock);
            Subscriptions = new SubscriptionService(Db, tenant, auth, materialiser, Clock);
            Reader = new EntitlementReader(Db, tenant, auth, Clock);
            Renewals = new SubscriptionRenewalService(Db, tenant, materialiser, Orders, Invoices, Payments, Clock);
        }

        public async Task<SubscriptionDto> SeedPaidSubscriptionAsync(
            decimal price = 19.99m,
            decimal stories = 8,
            string interval = BillingIntervals.Month)
        {
            await Catalogue.CreateMeterAsync(new CreateMeterRequest("stories", "Stories", MeterKinds.Counter, "stories"));
            var plan = await Catalogue.CreatePlanAsync(new CreatePlanRequest("family", "Family", interval));
            var draft = await Catalogue.CreateDraftVersionAsync(plan.Id, new CreatePlanVersionRequest(price, "GBP"));
            await Catalogue.SetEntitlementsAsync(draft.Id, new SetEntitlementsRequest(
                [new PlanEntitlementSpec("stories", stories, ResetPolicies.Period)]));
            await Catalogue.PublishVersionAsync(draft.Id);

            return await Subscriptions.SubscribeAsync(Subscriber(), "family", paymentMandateId: Guid.NewGuid());
        }

        /// <summary>Move past the period boundary so the subscription is due.</summary>
        public async Task AdvancePastPeriodEndAsync(Guid subscriptionId)
        {
            var s = await Db.Subscriptions.FirstAsync(x => x.Id == subscriptionId);
            Clock.UtcNow = s.CurrentPeriodEnd.AddMinutes(1);
        }
    }

    private static SubscriberRef Subscriber() => new(SubscriberKinds.Tenant, TenantId);

    [Fact]
    public async Task AnAnnualSubscription_Should_RenewAYearLater_NotAMonth()
    {
        var h = new Harness();
        var subscription = await h.SeedPaidSubscriptionAsync(stories: 8, interval: BillingIntervals.Year);

        var first = await h.Db.Subscriptions.AsNoTracking().FirstAsync(x => x.Id == subscription.Id);
        var firstEnd = first.CurrentPeriodEnd;

        await h.Renewals.RenewAsync(subscription.Id);
        await h.AdvancePastPeriodEndAsync(subscription.Id);
        await h.Renewals.RenewAsync(subscription.Id);

        var second = await h.Db.SubscriptionPeriods.AsNoTracking()
            .Where(p => p.SubscriptionId == subscription.Id)
            .OrderByDescending(p => p.Sequence)
            .FirstAsync();

        // Renewal used to add a month regardless of interval, so an annual subscriber was charged
        // the annual price every month after their first year — and their period entitlements
        // started expiring monthly with it.
        second.EndsAt.Should().Be(firstEnd.AddYears(1));
    }

    [Fact]
    public async Task APurchasedTopUp_Should_BeReadable_WithNoSubscription()
    {
        var h = new Harness();
        await h.Catalogue.CreateMeterAsync(new CreateMeterRequest("stories", "Stories", MeterKinds.Counter, "stories"));

        h.Db.EntitlementGrants.Add(new EntitlementGrant
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            SubscriberKind = SubscriberKinds.Tenant,
            SubscriberId = TenantId,
            MeterCode = "stories",
            Source = GrantSources.Purchase,
            Allowance = 20,
            Status = GrantStatuses.Open
        });
        await h.Db.SaveChangesAsync();

        var snapshot = await h.Reader.GetAsync(Subscriber());

        // Purchased grants are keyed to the SUBSCRIBER so they outlive subscriptions. Returning null
        // here made the documented pre-check report no allowance for someone holding paid-for units
        // that IUsageMeter would happily have funded.
        snapshot.Should().NotBeNull();
        snapshot!.SubscriptionId.Should().BeNull();
        snapshot.Meters.Should().ContainSingle()
            .Which.Remaining.Should().Be(20);
    }

    // ---- the happy path ---------------------------------------------------------------------

    [Fact]
    public async Task APaidSubscription_Should_GrantNothing_UntilItsFirstPeriodIsPaid()
    {
        var h = new Harness();
        await h.SeedPaidSubscriptionAsync(stories: 8);

        // Subscribing to a PAID plan confers nothing on its own. Only the free tier settles at
        // signup, because only a zero total can settle without money arriving.
        (await h.Db.EntitlementGrants.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task APaidPeriod_Should_Charge_Grant_AndCompleteItsOrder()
    {
        var h = new Harness();
        var subscription = await h.SeedPaidSubscriptionAsync(stories: 8);

        var outcome = await h.Renewals.RenewAsync(subscription.Id);

        outcome.Should().Be(RenewalOutcome.Settled);
        h.Invoices.Calls.Should().Be(1);
        h.Payments.Keys.Should().ContainSingle();

        // Orders are created Draft; leaving one there after the period was served would leave the
        // canonical record of the transaction contradicting what happened.
        h.Orders.Transitions.Should().ContainSingle(t => t.ToStatus == OrderStatusCodes.Complete);

        (await h.Reader.GetMeterAsync(Subscriber(), "stories"))!.Allowance.Should().Be(8);
    }

    [Fact]
    public async Task ASubscriptionWithNothingDue_Should_BeLeftAlone()
    {
        var h = new Harness();
        var subscription = await h.SeedPaidSubscriptionAsync();
        await h.Renewals.RenewAsync(subscription.Id);

        // Everything settled and the boundary not reached: a job running again must not mint a
        // period nobody owes anything for.
        (await h.Renewals.RenewAsync(subscription.Id)).Should().Be(RenewalOutcome.NothingToDo);
        h.Invoices.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ReRunningTheJob_Should_NotBillTwiceForOnePeriod()
    {
        var h = new Harness();
        var subscription = await h.SeedPaidSubscriptionAsync();
        await h.Renewals.RenewAsync(subscription.Id);

        await h.AdvancePastPeriodEndAsync(subscription.Id);
        await h.Renewals.RenewAsync(subscription.Id);
        await h.Renewals.RenewAsync(subscription.Id);

        // Two settled periods and a third call that found nothing. The anchor plus the
        // per-side-effect checks are what stop a re-run billing again.
        h.Invoices.Calls.Should().Be(2);
        (await h.Db.EntitlementGrants.CountAsync()).Should().Be(2, "one grant per settled period");
    }

    // ---- cancellation ------------------------------------------------------------------------

    [Fact]
    public async Task ASubscriptionCancelledAtPeriodEnd_Should_BeClosed_NotBilled()
    {
        var h = new Harness();
        var subscription = await h.SeedPaidSubscriptionAsync();
        await h.Renewals.RenewAsync(subscription.Id);

        await h.Subscriptions.CancelAsync(subscription.Id, atPeriodEnd: true);
        await h.AdvancePastPeriodEndAsync(subscription.Id);

        var invoicesBefore = h.Invoices.Calls;
        var outcome = await h.Renewals.RenewAsync(subscription.Id);

        // Checked before a period is even created. Selecting on status alone would bill a
        // subscriber who explicitly cancelled.
        outcome.Should().Be(RenewalOutcome.Closed);
        h.Invoices.Calls.Should().Be(invoicesBefore, "a cancelled subscription must not be billed again");
    }

    // ---- failure and retry --------------------------------------------------------------------

    [Fact]
    public async Task AWithdrawnMandate_Should_StopRatherThanLoop()
    {
        var h = new Harness();
        var subscription = await h.SeedPaidSubscriptionAsync();
        h.Payments.MandateGone = true;

        var outcome = await h.Renewals.RenewAsync(subscription.Id);

        outcome.Should().Be(RenewalOutcome.NeedsReauthorisation);

        var period = await h.Db.SubscriptionPeriods.OrderByDescending(p => p.Sequence).FirstAsync();
        period.Status.Should().Be(SubscriptionPeriodStatuses.Failed);

        // Null NextAttemptAt means "do not retry": no amount of retrying restores a withdrawn
        // authorisation, so this needs the customer, not the job.
        period.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public async Task AFailedPayment_Should_MaterialiseNoGrants()
    {
        var h = new Harness();
        var subscription = await h.SeedPaidSubscriptionAsync(stories: 8);
        h.Payments.MandateGone = true;

        await h.Renewals.RenewAsync(subscription.Id);

        // An unpaid period confers no allowance — which is the whole reason materialisation lives
        // in settlement rather than in creating the period.
        (await h.Db.EntitlementGrants.CountAsync()).Should().Be(0);
        (await h.Db.Subscriptions.FirstAsync()).Status.Should().Be(SubscriptionStatuses.PastDue);
    }

    [Fact]
    public async Task ARetry_Should_UseAFreshIntentKey()
    {
        var h = new Harness();
        var subscription = await h.SeedPaidSubscriptionAsync();

        h.Payments.MandateGone = true;
        await h.Renewals.RenewAsync(subscription.Id);

        // Make it retryable, as a soft decline would be.
        var period = await h.Db.SubscriptionPeriods.OrderByDescending(p => p.Sequence).FirstAsync();
        period.NextAttemptAt = h.Clock.UtcNow.AddDays(-1);
        period.OrderId ??= Guid.NewGuid();
        await h.Db.SaveChangesAsync();

        h.Payments.MandateGone = false;
        var outcome = await h.Renewals.RetryAsync(subscription.Id);

        outcome.Should().Be(RenewalOutcome.Settled);

        // A FRESH intent per attempt. PaymentService treats Failed as terminal, so reusing the
        // failed intent would strand every hard decline exactly where this should help.
        h.Payments.Keys.Should().ContainSingle().Which.Should().EndWith(":attempt:2");
    }

    [Fact]
    public async Task RetryingBeyondTheBoundedCount_Should_Expire()
    {
        var h = new Harness();
        var subscription = await h.SeedPaidSubscriptionAsync();

        h.Payments.MandateGone = true;
        await h.Renewals.RenewAsync(subscription.Id);

        var period = await h.Db.SubscriptionPeriods.OrderByDescending(p => p.Sequence).FirstAsync();
        period.AttemptCount = 9;
        period.NextAttemptAt = h.Clock.UtcNow.AddDays(-1);
        await h.Db.SaveChangesAsync();

        var outcome = await h.Renewals.RetryAsync(subscription.Id);

        // Leaving it in past_due forever would hide the subscription from every selector.
        outcome.Should().Be(RenewalOutcome.Expired);
        (await h.Db.Subscriptions.FirstAsync()).Status.Should().Be(SubscriptionStatuses.Expired);
    }

    // ---- pending plan changes ------------------------------------------------------------------

    [Fact]
    public async Task APendingUpgrade_Should_ApplyOnlyOnSettlement()
    {
        var h = new Harness();
        var subscription = await h.SeedPaidSubscriptionAsync(stories: 8);
        await h.Renewals.RenewAsync(subscription.Id);

        var bigger = await h.Catalogue.CreatePlanAsync(new CreatePlanRequest("studio", "Studio", BillingIntervals.Month));
        var draft = await h.Catalogue.CreateDraftVersionAsync(bigger.Id, new CreatePlanVersionRequest(39.99m, "GBP"));
        await h.Catalogue.SetEntitlementsAsync(draft.Id, new SetEntitlementsRequest(
            [new PlanEntitlementSpec("stories", 16, ResetPolicies.Period)]));
        await h.Catalogue.PublishVersionAsync(draft.Id);

        await h.Subscriptions.ChangePlanAsync(subscription.Id, "studio");

        // Before renewal: still on the paid-for plan.
        (await h.Reader.GetMeterAsync(Subscriber(), "stories"))!.Allowance.Should().Be(8);

        await h.AdvancePastPeriodEndAsync(subscription.Id);
        await h.Renewals.RenewAsync(subscription.Id);

        var reloaded = await h.Db.Subscriptions.AsNoTracking().FirstAsync();
        reloaded.PendingPlanVersionId.Should().BeNull("it was applied and cleared");
        reloaded.PlanVersionId.Should().Be(draft.Id);
    }

    [Fact]
    public async Task AFailedUpgrade_Should_LeaveThePendingVersionUnapplied()
    {
        var h = new Harness();
        var subscription = await h.SeedPaidSubscriptionAsync(stories: 8);
        await h.Renewals.RenewAsync(subscription.Id);

        var bigger = await h.Catalogue.CreatePlanAsync(new CreatePlanRequest("studio", "Studio", BillingIntervals.Month));
        var draft = await h.Catalogue.CreateDraftVersionAsync(bigger.Id, new CreatePlanVersionRequest(39.99m, "GBP"));
        await h.Catalogue.SetEntitlementsAsync(draft.Id, new SetEntitlementsRequest(
            [new PlanEntitlementSpec("stories", 16, ResetPolicies.Period)]));
        await h.Catalogue.PublishVersionAsync(draft.Id);

        await h.Subscriptions.ChangePlanAsync(subscription.Id, "studio");
        await h.AdvancePastPeriodEndAsync(subscription.Id);
        h.Payments.MandateGone = true;

        await h.Renewals.RenewAsync(subscription.Id);

        // An unpaid upgrade confers nothing: the pending version is still pending, and the pinned
        // version is still the one that was actually paid for and can be recovered.
        var reloaded = await h.Db.Subscriptions.AsNoTracking().FirstAsync();
        reloaded.PendingPlanVersionId.Should().Be(draft.Id);
        reloaded.PlanVersionId.Should().NotBe(draft.Id);
        reloaded.Status.Should().Be(SubscriptionStatuses.PastDue);

        // Allowance is zero, but for the ordinary reason rather than the upgrade: period one's
        // grant expired at its boundary and the new period was never paid for. The subscriber is
        // NOT silently on the bigger plan.
        (await h.Reader.GetMeterAsync(Subscriber(), "stories"))!.Allowance.Should().Be(0);
        (await h.Db.EntitlementGrants.CountAsync()).Should().Be(1, "only the paid-for period ever granted");
    }
}
