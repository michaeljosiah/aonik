using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
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
/// Spec 087 P3 acceptance: <b>a £0 plan works end to end — granted and consumable.</b>
///
/// Rev 1 of the spec claimed this while grants and usage arrived a phase later, so a P3 subscriber
/// could be granted nothing and consume nothing. These tests are what make the criterion mean
/// something.
/// </summary>
public class FreeTierEndToEndTests
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

    /// <summary>Authorises the tenant-kind subscriber used throughout.</summary>
    private sealed class AllowTenantAuthorizer : ISubscriberAuthorizer
    {
        public IReadOnlyCollection<string> SupportedKinds => [SubscriberKinds.Tenant];
        public Task<bool> CanActForAsync(SubscriberRef s, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class DenyPartyAuthorizer : ISubscriberAuthorizer
    {
        public IReadOnlyCollection<string> SupportedKinds => [SubscriberKinds.Party];
        public Task<bool> CanActForAsync(SubscriberRef s, CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class Harness
    {
        public SubscriptionsDbContext Db { get; }
        public TestClock Clock { get; } = new();
        public CatalogueService Catalogue { get; }
        public SubscriptionService Subscriptions { get; }
        public EntitlementReader Reader { get; }
        public UsageMeter Meter { get; }

        public Harness(params ISubscriberAuthorizer[] authorizers)
        {
            Db = new SubscriptionsDbContext(
                new DbContextOptionsBuilder<SubscriptionsDbContext>()
                    .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                    .Options,
                new TestTenantProvider());

            var tenant = new TestTenantProvider();
            var auth = new SubscriberAuthorization(
                authorizers.Length > 0 ? authorizers : [new AllowTenantAuthorizer()]);

            Catalogue = new CatalogueService(Db, tenant, Clock);
            Subscriptions = new SubscriptionService(Db, tenant, auth, new EntitlementMaterialiser(Db, Clock), Clock);
            Reader = new EntitlementReader(Db, tenant, auth, Clock);
            Meter = new UsageMeter(Db, tenant, auth, Clock);
        }

        /// <summary>The Arke Kids "Peek" tier: free forever, one story to keep.</summary>
        public async Task SeedFreeTierAsync(decimal stories = 1, string reset = ResetPolicies.Period)
        {
            await Catalogue.CreateMeterAsync(new CreateMeterRequest("stories", "Stories", MeterKinds.Counter, "stories"));
            var plan = await Catalogue.CreatePlanAsync(new CreatePlanRequest("peek", "Peek", BillingIntervals.None));
            var draft = await Catalogue.CreateDraftVersionAsync(plan.Id, new CreatePlanVersionRequest(0m, "GBP"));
            await Catalogue.SetEntitlementsAsync(draft.Id, new SetEntitlementsRequest(
                [new PlanEntitlementSpec("stories", stories, reset)]));
            await Catalogue.PublishVersionAsync(draft.Id);
        }
    }

    private static SubscriberRef Subscriber() => new(SubscriberKinds.Tenant, TenantId);

    [Fact]
    public async Task AFreePlan_Should_Subscribe_Grant_And_Consume_WithNoPayment()
    {
        var h = new Harness();
        await h.SeedFreeTierAsync(stories: 3);

        // Subscribe — no mandate, because a £0 plan needs no way to be charged.
        var subscription = await h.Subscriptions.SubscribeAsync(Subscriber(), "peek");
        subscription.Status.Should().Be(SubscriptionStatuses.Active);

        // GRANTED. The period settled directly: no invoice, no intent, no journal entry, because
        // LedgerPostingService rejects non-positive amounts and nothing was earned anyway.
        var snapshot = await h.Reader.GetAsync(Subscriber());
        snapshot!.Meters.Should().ContainSingle();
        snapshot.Meters[0].Remaining.Should().Be(3);

        // CONSUMABLE.
        var hold = await h.Meter.ReserveAsync(Subscriber(), "stories", 1, "story-1");
        await h.Meter.CommitAsync(hold.ReservationId, 1, new UsageSource("Story", Guid.NewGuid()));

        (await h.Reader.GetMeterAsync(Subscriber(), "stories"))!.Remaining.Should().Be(2);
    }

    [Fact]
    public async Task Allowance_Should_BeRefusedAtZero_NeverGoingNegative()
    {
        var h = new Harness();
        await h.SeedFreeTierAsync(stories: 1);
        await h.Subscriptions.SubscribeAsync(Subscriber(), "peek");

        var hold = await h.Meter.ReserveAsync(Subscriber(), "stories", 1, "story-1");
        await h.Meter.CommitAsync(hold.ReservationId, 1, new UsageSource("Story", Guid.NewGuid()));

        var act = async () => await h.Meter.ReserveAsync(Subscriber(), "stories", 1, "story-2");

        // Overage is an explicit purchase, never an implicit debt.
        var thrown = await act.Should().ThrowAsync<EntitlementExceededException>();
        thrown.Which.Available.Should().Be(0);
    }

    [Fact]
    public async Task AHeldReservation_Should_ReduceAvailability_BeforeItIsCommitted()
    {
        var h = new Harness();
        await h.SeedFreeTierAsync(stories: 2);
        await h.Subscriptions.SubscribeAsync(Subscriber(), "peek");

        await h.Meter.ReserveAsync(Subscriber(), "stories", 2, "batch-1");

        // The hold is what makes the concurrency check engage at all — without touching the grant,
        // two concurrent reservations would both take the last unit.
        var meter = await h.Reader.GetMeterAsync(Subscriber(), "stories");
        meter!.Held.Should().Be(2);
        meter.Remaining.Should().Be(0);
    }

    [Fact]
    public async Task ReleasingAHold_Should_ReturnTheAllowance()
    {
        var h = new Harness();
        await h.SeedFreeTierAsync(stories: 2);
        await h.Subscriptions.SubscribeAsync(Subscriber(), "peek");

        var hold = await h.Meter.ReserveAsync(Subscriber(), "stories", 2, "batch-1");
        await h.Meter.ReleaseAsync(hold.ReservationId);

        (await h.Reader.GetMeterAsync(Subscriber(), "stories"))!.Remaining.Should().Be(2);
    }

    [Fact]
    public async Task AShortCommit_Should_ReturnTheUnusedRemainder()
    {
        var h = new Harness();
        await h.SeedFreeTierAsync(stories: 5);
        await h.Subscriptions.SubscribeAsync(Subscriber(), "peek");

        var hold = await h.Meter.ReserveAsync(Subscriber(), "stories", 5, "batch-1");
        var result = await h.Meter.CommitAsync(hold.ReservationId, 2, new UsageSource("Story", Guid.NewGuid()));

        // The work cost less than expected; the difference goes back, not into consumption.
        result.QuantityCommitted.Should().Be(2);
        (await h.Reader.GetMeterAsync(Subscriber(), "stories"))!.Remaining.Should().Be(3);
    }

    [Fact]
    public async Task ReserveAsync_Should_BeIdempotent_OnItsKey()
    {
        var h = new Harness();
        await h.SeedFreeTierAsync(stories: 3);
        await h.Subscriptions.SubscribeAsync(Subscriber(), "peek");

        var first = await h.Meter.ReserveAsync(Subscriber(), "stories", 1, "story-1");
        var second = await h.Meter.ReserveAsync(Subscriber(), "stories", 1, "story-1");

        second.ReservationId.Should().Be(first.ReservationId);
        (await h.Reader.GetMeterAsync(Subscriber(), "stories"))!.Held.Should().Be(1, "a replay must not take a second hold");
    }

    [Fact]
    public async Task CommitAsync_Should_Refuse_AnExpiredHold()
    {
        var h = new Harness();
        await h.SeedFreeTierAsync(stories: 3);
        await h.Subscriptions.SubscribeAsync(Subscriber(), "peek");

        var hold = await h.Meter.ReserveAsync(Subscriber(), "stories", 1, "story-1", holdFor: TimeSpan.FromMinutes(5));
        h.Clock.UtcNow = h.Clock.UtcNow.AddHours(1);

        var act = async () => await h.Meter.CommitAsync(hold.ReservationId, 1, new UsageSource("Story", Guid.NewGuid()));
        await act.Should().ThrowAsync<InvalidStateException>().WithMessage("*expired*");
    }

    [Fact]
    public async Task CommitAsync_Should_Refuse_MoreThanWasReserved()
    {
        var h = new Harness();
        await h.SeedFreeTierAsync(stories: 5);
        await h.Subscriptions.SubscribeAsync(Subscriber(), "peek");

        var hold = await h.Meter.ReserveAsync(Subscriber(), "stories", 1, "story-1");

        // The extra was never held, so nothing guarantees it is available.
        var act = async () => await h.Meter.CommitAsync(hold.ReservationId, 3, new UsageSource("Story", Guid.NewGuid()));
        await act.Should().ThrowAsync<InvalidStateException>().WithMessage("*exceeds the reserved*");
    }

    [Fact]
    public async Task ANeverResettingEntitlement_Should_ProduceANonExpiringGrant()
    {
        var h = new Harness();
        await h.SeedFreeTierAsync(stories: 1, reset: ResetPolicies.Never);
        await h.Subscriptions.SubscribeAsync(Subscriber(), "peek");

        var grant = await h.Db.EntitlementGrants.AsNoTracking().SingleAsync();

        // Expiry follows the RESET POLICY, not the grant's source. Deriving it from `plan` alone
        // would discard an accumulating allowance at every renewal.
        grant.ExpiresAt.Should().BeNull();
    }

    // ---- the security boundary --------------------------------------------------------------

    [Fact]
    public async Task ACallerWhoMayNotActForTheSubscriber_Should_BeRefused()
    {
        var h = new Harness(new DenyPartyAuthorizer());
        await h.SeedFreeTierAsync();

        var act = async () => await h.Subscriptions.SubscribeAsync(
            new SubscriberRef(SubscriberKinds.Party, Guid.NewGuid()), "peek");

        // Tenant scoping alone does not authorise acting for a particular subscriber — without
        // this, any caller could subscribe or consume for another family in the same tenant.
        await act.Should().ThrowAsync<PermissionDeniedException>();
    }

    [Fact]
    public async Task AnUnregisteredSubscriberKind_Should_FailClosed()
    {
        var h = new Harness(new AllowTenantAuthorizer());
        await h.SeedFreeTierAsync();

        var act = async () => await h.Subscriptions.SubscribeAsync(
            new SubscriberRef("group", Guid.NewGuid()), "peek");

        // An unknown kind is not a reason to allow the call.
        await act.Should().ThrowAsync<PermissionDeniedException>().WithMessage("*No authorizer*");
    }

    [Fact]
    public async Task TwoAuthorizersClaimingOneKind_Should_Throw()
    {
        var act = () => new SubscriberAuthorization([new AllowTenantAuthorizer(), new AllowTenantAuthorizer()]);

        // Two answers to "may this caller act" is an ambiguity about authorisation, not a tie.
        act.Should().Throw<InvalidOperationException>().WithMessage("*more than one*");
    }

    // ---- lifecycle ---------------------------------------------------------------------------

    [Fact]
    public async Task ASecondActiveSubscription_Should_BeRefused()
    {
        var h = new Harness();
        await h.SeedFreeTierAsync();
        await h.Subscriptions.SubscribeAsync(Subscriber(), "peek");

        var act = async () => await h.Subscriptions.SubscribeAsync(Subscriber(), "peek");
        await act.Should().ThrowAsync<InvalidStateException>().WithMessage("*already holds an active subscription*");
    }

    [Fact]
    public async Task CancellingAtPeriodEnd_Should_LeaveTheSubscriptionUsableUntilTheBoundary()
    {
        var h = new Harness();
        await h.SeedFreeTierAsync(stories: 2);
        var subscription = await h.Subscriptions.SubscribeAsync(Subscriber(), "peek");

        var cancelled = await h.Subscriptions.CancelAsync(subscription.Id, atPeriodEnd: true);

        cancelled.CancelAtPeriodEnd.Should().BeTrue();
        cancelled.Status.Should().Be(SubscriptionStatuses.Active, "the subscriber keeps what they have paid for");

        // Still consumable until the boundary; the renewal job closes it rather than billing again.
        var hold = await h.Meter.ReserveAsync(Subscriber(), "stories", 1, "story-1");
        await h.Meter.CommitAsync(hold.ReservationId, 1, new UsageSource("Story", Guid.NewGuid()));
    }

    [Fact]
    public async Task APricedPlanWithNoMandate_Should_BeRefusedAtSubscribe()
    {
        var h = new Harness();
        await h.Catalogue.CreateMeterAsync(new CreateMeterRequest("stories", "Stories", MeterKinds.Counter, "stories"));
        var plan = await h.Catalogue.CreatePlanAsync(new CreatePlanRequest("family", "Family", BillingIntervals.Month));
        var draft = await h.Catalogue.CreateDraftVersionAsync(plan.Id, new CreatePlanVersionRequest(19.99m, "GBP"));
        await h.Catalogue.SetEntitlementsAsync(draft.Id, new SetEntitlementsRequest(
            [new PlanEntitlementSpec("stories", 8, ResetPolicies.Period)]));
        await h.Catalogue.PublishVersionAsync(draft.Id);

        var act = async () => await h.Subscriptions.SubscribeAsync(Subscriber(), "family");

        // Otherwise it would renew straight into past_due with no way to recover.
        await act.Should().ThrowAsync<InvalidStateException>().WithMessage("*requires a payment mandate*");
    }

    [Fact]
    public async Task APendingPlanChange_Should_NotAlterWhatIsReadableNow()
    {
        var h = new Harness();
        await h.SeedFreeTierAsync(stories: 1);
        var subscription = await h.Subscriptions.SubscribeAsync(Subscriber(), "peek");

        var bigger = await h.Catalogue.CreatePlanAsync(new CreatePlanRequest("spark", "Spark", BillingIntervals.None));
        var draft = await h.Catalogue.CreateDraftVersionAsync(bigger.Id, new CreatePlanVersionRequest(0m, "GBP"));
        await h.Catalogue.SetEntitlementsAsync(draft.Id, new SetEntitlementsRequest(
            [new PlanEntitlementSpec("stories", 99, ResetPolicies.Period)]));
        await h.Catalogue.PublishVersionAsync(draft.Id);

        await h.Subscriptions.ChangePlanAsync(subscription.Id, "spark");

        // An unpaid upgrade confers nothing: the reader reports the last SETTLED state.
        (await h.Reader.GetMeterAsync(Subscriber(), "stories"))!.Allowance.Should().Be(1);
    }
}
