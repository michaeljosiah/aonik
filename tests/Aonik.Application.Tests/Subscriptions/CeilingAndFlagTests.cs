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
/// Spec 087 P4 — the two meter kinds that are not consumed.
///
/// A ceiling is <b>claimed and released</b>, so deleting the held object returns the slot; a
/// counter is spent, so deleting a story must NOT return allowance. That difference is why they
/// are separate kinds rather than one "consumed" model, and it is what these cover.
/// </summary>
public class CeilingAndFlagTests
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
        public Task<bool> CanManageBillingForAsync(SubscriberRef s, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class Harness
    {
        public SubscriptionsDbContext Db { get; }
        public CatalogueService Catalogue { get; }
        public SubscriptionService Subscriptions { get; }
        public EntitlementReader Reader { get; }
        public UsageMeter Meter { get; }

        public Harness()
        {
            var clock = new TestClock();
            Db = new SubscriptionsDbContext(
                new DbContextOptionsBuilder<SubscriptionsDbContext>()
                    .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                    .Options,
                new TestTenantProvider());

            var tenant = new TestTenantProvider();
            var auth = new SubscriberAuthorization([new AllowTenantAuthorizer()]);

            Catalogue = new CatalogueService(Db, tenant, clock);
            Subscriptions = new SubscriptionService(Db, tenant, auth, new EntitlementMaterialiser(Db, clock), clock);
            Reader = new EntitlementReader(Db, tenant, auth, clock);
            Meter = new UsageMeter(Db, tenant, auth, clock);
        }

        public async Task SeedAsync(decimal stories = 3, decimal profiles = 2, decimal hd = 1)
        {
            await Catalogue.CreateMeterAsync(new CreateMeterRequest("stories", "Stories", MeterKinds.Counter, "stories"));
            await Catalogue.CreateMeterAsync(new CreateMeterRequest("child-profiles", "Child profiles", MeterKinds.Ceiling, "profiles"));
            await Catalogue.CreateMeterAsync(new CreateMeterRequest("hd-styles", "HD styles", MeterKinds.Flag));

            var plan = await Catalogue.CreatePlanAsync(new CreatePlanRequest("peek", "Peek", BillingIntervals.None));
            var draft = await Catalogue.CreateDraftVersionAsync(plan.Id, new CreatePlanVersionRequest(0m, "GBP"));
            await Catalogue.SetEntitlementsAsync(draft.Id, new SetEntitlementsRequest(
            [
                new PlanEntitlementSpec("stories", stories, ResetPolicies.Period),
                new PlanEntitlementSpec("child-profiles", profiles, ResetPolicies.Never),
                new PlanEntitlementSpec("hd-styles", hd, ResetPolicies.Never)
            ]));
            await Catalogue.PublishVersionAsync(draft.Id);
            await Subscriptions.SubscribeAsync(Subscriber(), "peek");
        }
    }

    private static SubscriberRef Subscriber() => new(SubscriberKinds.Tenant, TenantId);

    // ---- the distinction that justifies two kinds -------------------------------------------

    [Fact]
    public async Task DeletingACeilingObject_Should_ReturnItsSlot()
    {
        var h = new Harness();
        await h.SeedAsync(profiles: 2);

        await h.Meter.ClaimSlotAsync(Subscriber(), "child-profiles", "profile-1");
        await h.Meter.ClaimSlotAsync(Subscriber(), "child-profiles", "profile-2");

        var full = async () => await h.Meter.ClaimSlotAsync(Subscriber(), "child-profiles", "profile-3");
        await full.Should().ThrowAsync<EntitlementExceededException>();

        await h.Meter.ReleaseSlotAsync(Subscriber(), "child-profiles", "profile-2");

        // Room again — a ceiling is held, not spent.
        var act = async () => await h.Meter.ClaimSlotAsync(Subscriber(), "child-profiles", "profile-3");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ConsumingACounter_Should_NotBeReturnableByDeletion()
    {
        var h = new Harness();
        await h.SeedAsync(stories: 1);

        var hold = await h.Meter.ReserveAsync(Subscriber(), "stories", 1, "story-1");
        await h.Meter.CommitAsync(hold.ReservationId, 1, new UsageSource("Story", Guid.NewGuid()));

        // There is deliberately no "un-consume". Tidying up must not hand allowance back, or a
        // subscriber could make unlimited stories by deleting each one.
        (await h.Reader.GetMeterAsync(Subscriber(), "stories"))!.Remaining.Should().Be(0);
    }

    // ---- idempotency ------------------------------------------------------------------------

    [Fact]
    public async Task ClaimingTwiceForOneHolder_Should_NotConsumeASecondSlot()
    {
        var h = new Harness();
        await h.SeedAsync(profiles: 2);

        await h.Meter.ClaimSlotAsync(Subscriber(), "child-profiles", "profile-1");
        await h.Meter.ClaimSlotAsync(Subscriber(), "child-profiles", "profile-1");

        // A retried create must not permanently lose a slot.
        (await h.Db.CeilingHoldings.AsNoTracking().SingleAsync()).Held.Should().Be(1);
    }

    [Fact]
    public async Task ReleasingTwiceForOneHolder_Should_NotFreeASecondSlot()
    {
        var h = new Harness();
        await h.SeedAsync(profiles: 2);

        await h.Meter.ClaimSlotAsync(Subscriber(), "child-profiles", "profile-1");
        await h.Meter.ClaimSlotAsync(Subscriber(), "child-profiles", "profile-2");

        await h.Meter.ReleaseSlotAsync(Subscriber(), "child-profiles", "profile-1");
        await h.Meter.ReleaseSlotAsync(Subscriber(), "child-profiles", "profile-1");

        // A retried delete must not admit more objects than the ceiling.
        (await h.Db.CeilingHoldings.AsNoTracking().SingleAsync()).Held.Should().Be(1);
    }

    [Fact]
    public async Task ReleasingAnUnknownHolder_Should_BeANoOp()
    {
        var h = new Harness();
        await h.SeedAsync(profiles: 2);
        await h.Meter.ClaimSlotAsync(Subscriber(), "child-profiles", "profile-1");

        await h.Meter.ReleaseSlotAsync(Subscriber(), "child-profiles", "never-claimed");

        (await h.Db.CeilingHoldings.AsNoTracking().SingleAsync()).Held.Should().Be(1);
    }

    [Fact]
    public async Task ClaimSlotAsync_Should_RequireAHolderReference()
    {
        var h = new Harness();
        await h.SeedAsync();

        // Without a holder identity nothing downstream can be idempotent.
        var act = async () => await h.Meter.ClaimSlotAsync(Subscriber(), "child-profiles", "  ");
        await act.Should().ThrowAsync<InvalidStateException>().WithMessage("*holder reference*");
    }

    // ---- flags -------------------------------------------------------------------------------

    [Fact]
    public async Task HasFlagAsync_Should_ReflectThePlan()
    {
        var h = new Harness();
        await h.SeedAsync(hd: 1);

        (await h.Meter.HasFlagAsync(Subscriber(), "hd-styles")).Should().BeTrue();
    }

    [Fact]
    public async Task HasFlagAsync_Should_BeFalse_When_ThePlanTurnsItOff()
    {
        var h = new Harness();
        await h.SeedAsync(hd: 0);

        (await h.Meter.HasFlagAsync(Subscriber(), "hd-styles")).Should().BeFalse();
    }

    [Fact]
    public async Task HasFlagAsync_Should_BeFalse_ForAMeterThePlanDoesNotGrant()
    {
        var h = new Harness();
        await h.SeedAsync();

        (await h.Meter.HasFlagAsync(Subscriber(), "early-access")).Should().BeFalse();
    }

    [Fact]
    public async Task CeilingsAndFlags_Should_NotProduceGrants()
    {
        var h = new Harness();
        await h.SeedAsync();

        // Only counters are drawn down. Granting a ceiling or a flag would make "remaining"
        // meaningless for both.
        var grants = await h.Db.EntitlementGrants.AsNoTracking().ToListAsync();
        grants.Should().OnlyContain(g => g.MeterCode == "stories");
    }

    [Fact]
    public async Task ASubscriberWithNoSubscription_Should_HoldNoSlots()
    {
        var h = new Harness();
        await h.SeedAsync();

        var subscriber = Subscriber();
        var current = await h.Subscriptions.GetForSubscriberAsync(subscriber);
        await h.Subscriptions.CancelAsync(current!.Id, atPeriodEnd: false);

        // No plan means zero slots, not unlimited.
        var act = async () => await h.Meter.ClaimSlotAsync(subscriber, "child-profiles", "profile-1");
        await act.Should().ThrowAsync<EntitlementExceededException>();
    }
}
