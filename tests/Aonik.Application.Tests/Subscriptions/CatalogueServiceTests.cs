using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Subscriptions.Contracts.Models;
using Aonik.Subscriptions.Persistence;
using Aonik.Subscriptions.Services.Catalogue;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Subscriptions;

/// <summary>
/// Spec 087 P2 acceptance: a plan expressing all three entitlement kinds can be authored and read,
/// the meter table is the authority for meter codes, and a published version is frozen.
/// </summary>
public class CatalogueServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private sealed class TestTenantProvider : ITenantProvider
    {
        public Guid GetCurrentTenantId() => TenantId;
        public bool TryGetCurrentTenantId(out Guid tenantId) { tenantId = TenantId; return true; }
    }

    private sealed class TestCurrentUserProvider : ICurrentUserProvider
    {
        private static readonly Guid UserId = Guid.NewGuid();
        public Guid? GetCurrentUserId() => UserId;
        public bool TryGetCurrentUserId(out Guid userId) { userId = UserId; return true; }
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
    }

    private static CatalogueService CreateService(out SubscriptionsDbContext dbContext)
    {
        var options = new DbContextOptionsBuilder<SubscriptionsDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        dbContext = new SubscriptionsDbContext(
            options,
            new TestTenantProvider(),
            new TestCurrentUserProvider(),
            new TestClock());

        return new CatalogueService(dbContext, new TestTenantProvider(), new TestClock());
    }

    private static async Task SeedKidsMetersAsync(CatalogueService service)
    {
        await service.CreateMeterAsync(new CreateMeterRequest("stories", "Stories", MeterKinds.Counter, "stories"));
        await service.CreateMeterAsync(new CreateMeterRequest("animated-videos", "Animated videos", MeterKinds.Counter, "videos"));
        await service.CreateMeterAsync(new CreateMeterRequest("child-profiles", "Child profiles", MeterKinds.Ceiling, "profiles"));
        await service.CreateMeterAsync(new CreateMeterRequest("hd-styles", "Every art style in HD", MeterKinds.Flag));
    }

    [Fact]
    public async Task Catalogue_Should_ExpressAllThreeEntitlementKinds_When_AuthoringARealPricingTier()
    {
        // Arrange — the "Family" tier: 8 stories a month, 2 videos a month, up to 3 profiles, HD on.
        var service = CreateService(out _);
        await SeedKidsMetersAsync(service);

        var plan = await service.CreatePlanAsync(new CreatePlanRequest("family", "Family", BillingIntervals.Month));
        var draft = await service.CreateDraftVersionAsync(plan.Id, new CreatePlanVersionRequest(19.99m, "GBP"));

        // Act
        await service.SetEntitlementsAsync(draft.Id, new SetEntitlementsRequest(
        [
            new PlanEntitlementSpec("stories", 8, ResetPolicies.Period),
            new PlanEntitlementSpec("animated-videos", 2, ResetPolicies.Period),
            new PlanEntitlementSpec("child-profiles", 3, ResetPolicies.Never),
            new PlanEntitlementSpec("hd-styles", 1, ResetPolicies.Never)
        ]));

        var published = await service.PublishVersionAsync(draft.Id);

        // Assert
        published.Status.Should().Be(PlanVersionStatuses.Published);
        published.Entitlements.Should().HaveCount(4);
        published.Entitlements.Select(e => e.MeterKind).Distinct()
            .Should().BeEquivalentTo([MeterKinds.Counter, MeterKinds.Ceiling, MeterKinds.Flag]);

        // Kind and unit are read back from the meter, never stored on the entitlement.
        published.Entitlements.Single(e => e.MeterCode == "child-profiles").MeterKind.Should().Be(MeterKinds.Ceiling);
        published.Entitlements.Single(e => e.MeterCode == "stories").MeterUnit.Should().Be("stories");

        // Publishing the first version makes the plan offerable.
        var reloaded = await service.GetPlanByCodeAsync("family");
        reloaded!.Status.Should().Be(PlanStatuses.Active);
        reloaded.Versions.Should().ContainSingle();
    }

    [Fact]
    public async Task SetEntitlementsAsync_Should_Reject_When_MeterCodeIsNotRegistered()
    {
        var service = CreateService(out _);
        var plan = await service.CreatePlanAsync(new CreatePlanRequest("spark", "Spark", BillingIntervals.Month));
        var draft = await service.CreateDraftVersionAsync(plan.Id, new CreatePlanVersionRequest(9.99m, "GBP"));

        var act = async () => await service.SetEntitlementsAsync(draft.Id, new SetEntitlementsRequest(
            [new PlanEntitlementSpec("storeis", 6, ResetPolicies.Period)]));

        // The meter table is the authority — a typo fails closed rather than being discovered later.
        await act.Should().ThrowAsync<NotFoundException>().WithMessage("*storeis*");
    }

    [Fact]
    public async Task PublishedVersion_Should_BeImmutable_So_ExistingSubscribersAreNotRepriced()
    {
        var service = CreateService(out _);
        await SeedKidsMetersAsync(service);

        var plan = await service.CreatePlanAsync(new CreatePlanRequest("spark", "Spark", BillingIntervals.Month));
        var draft = await service.CreateDraftVersionAsync(plan.Id, new CreatePlanVersionRequest(9.99m, "GBP"));
        await service.SetEntitlementsAsync(draft.Id, new SetEntitlementsRequest(
            [new PlanEntitlementSpec("stories", 6, ResetPolicies.Period)]));
        await service.PublishVersionAsync(draft.Id);

        var act = async () => await service.SetEntitlementsAsync(draft.Id, new SetEntitlementsRequest(
            [new PlanEntitlementSpec("stories", 60, ResetPolicies.Period)]));

        await act.Should().ThrowAsync<InvalidStateException>().WithMessage("*can no longer be changed*");
    }

    [Fact]
    public async Task PublishVersionAsync_Should_SupersedeThePreviousVersion_And_KeepBothReadable()
    {
        var service = CreateService(out _);
        await SeedKidsMetersAsync(service);

        var plan = await service.CreatePlanAsync(new CreatePlanRequest("spark", "Spark", BillingIntervals.Month));

        var v1 = await service.CreateDraftVersionAsync(plan.Id, new CreatePlanVersionRequest(9.99m, "GBP"));
        await service.SetEntitlementsAsync(v1.Id, new SetEntitlementsRequest(
            [new PlanEntitlementSpec("stories", 6, ResetPolicies.Period)]));
        await service.PublishVersionAsync(v1.Id);

        var v2 = await service.CreateDraftVersionAsync(plan.Id, new CreatePlanVersionRequest(12.99m, "GBP"));
        await service.SetEntitlementsAsync(v2.Id, new SetEntitlementsRequest(
            [new PlanEntitlementSpec("stories", 8, ResetPolicies.Period)]));
        await service.PublishVersionAsync(v2.Id);

        // A subscription pinned to v1 must still be able to resolve it at its original price.
        var oldVersion = await service.GetVersionAsync(v1.Id);
        oldVersion!.Status.Should().Be(PlanVersionStatuses.Superseded);
        oldVersion.Price.Should().Be(9.99m);

        var current = await service.GetCurrentVersionAsync(plan.Id);
        current!.Version.Should().Be(2);
        current.Price.Should().Be(12.99m);
    }

    [Fact]
    public async Task CreateDraftVersionAsync_Should_Reject_When_ADraftAlreadyExists()
    {
        var service = CreateService(out _);
        var plan = await service.CreatePlanAsync(new CreatePlanRequest("studio", "Studio", BillingIntervals.Month));
        await service.CreateDraftVersionAsync(plan.Id, new CreatePlanVersionRequest(39.99m, "GBP"));

        var act = async () => await service.CreateDraftVersionAsync(plan.Id, new CreatePlanVersionRequest(44.99m, "GBP"));

        // Two concurrent drafts would race for the same version number.
        await act.Should().ThrowAsync<InvalidStateException>().WithMessage("*already has a draft*");
    }

    [Fact]
    public async Task SetEntitlementsAsync_Should_ValidateAllowanceAgainstTheMetersKind()
    {
        var service = CreateService(out _);
        await SeedKidsMetersAsync(service);

        var plan = await service.CreatePlanAsync(new CreatePlanRequest("peek", "Peek", BillingIntervals.None));
        var draft = await service.CreateDraftVersionAsync(plan.Id, new CreatePlanVersionRequest(0m, "GBP"));

        // A flag is on or off; 3 is meaningless and would be silently reinterpreted downstream.
        var flagAct = async () => await service.SetEntitlementsAsync(draft.Id, new SetEntitlementsRequest(
            [new PlanEntitlementSpec("hd-styles", 3, ResetPolicies.Never)]));
        await flagAct.Should().ThrowAsync<InvalidStateException>().WithMessage("*flag*");

        // A ceiling counts whole slots.
        var ceilingAct = async () => await service.SetEntitlementsAsync(draft.Id, new SetEntitlementsRequest(
            [new PlanEntitlementSpec("child-profiles", 2.5m, ResetPolicies.Never)]));
        await ceilingAct.Should().ThrowAsync<InvalidStateException>().WithMessage("*whole number*");
    }

    [Fact]
    public async Task FreeTier_Should_BeAnOrdinaryPlan_At_ZeroPrice()
    {
        // The Arke Kids "Peek" tier: free forever, one story. No trial machinery required.
        var service = CreateService(out _);
        await SeedKidsMetersAsync(service);

        var plan = await service.CreatePlanAsync(new CreatePlanRequest("peek", "Peek", BillingIntervals.None));
        var draft = await service.CreateDraftVersionAsync(plan.Id, new CreatePlanVersionRequest(0m, "GBP"));
        await service.SetEntitlementsAsync(draft.Id, new SetEntitlementsRequest(
            [new PlanEntitlementSpec("stories", 1, ResetPolicies.Never)]));

        var published = await service.PublishVersionAsync(draft.Id);

        published.Price.Should().Be(0m);
        published.Entitlements.Should().ContainSingle().Which.Allowance.Should().Be(1);
    }
}
