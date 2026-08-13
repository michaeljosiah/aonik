using Aonik.Platform.Entities.Party;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Consent;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Platform;

/// <summary>
/// Spec 095 §11 — the transition most products never build and are therefore quietly wrong about.
///
/// <para>
/// The assertion that matters most is the one about what does <em>not</em> happen at the consent
/// age: the guardian edge stays active. Collapsing the two boundaries would strip a parent's
/// authority over their own 14-year-old, five years early, and it would look like the feature
/// working.
/// </para>
/// </summary>
public class AgeTransitionTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 8, 13, 2, 15, 0, DateTimeKind.Utc);

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; } = Now;
    }

    private static PlatformDbContext CreateDbContext()
        => new(
            new DbContextOptionsBuilder<PlatformDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                .Options,
            new TestTenantProvider(TenantId),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new TestClock());

    private static AgeTransitionService CreateService(PlatformDbContext context)
        => new(
            context,
            new TestTenantProvider(TenantId),
            new TestClock(),
            NullLogger<AgeTransitionService>.Instance);

    private static Guid SeedChild(
        PlatformDbContext context,
        DateTime? consentAgeOn = null,
        DateTime? majorityOn = null,
        DateTime? safetyBandChangesOn = null,
        string consentBand = PartyConsentBands.BelowThreshold,
        string safetyBand = PartySafetyBands.Age6To9,
        DateTime? noticeSentOn = null)
    {
        var child = new global::Aonik.Platform.Entities.Party.Party
        {
            Id = Guid.NewGuid(), TenantId = TenantId, DisplayName = "A Child",
            PartyType = "Person", Status = "Active",
            BirthYear = 2018,
            ConsentBand = consentBand,
            SafetyBand = safetyBand,
            ConsentAgeOn = consentAgeOn,
            MajorityOn = majorityOn,
            SafetyBandChangesOn = safetyBandChangesOn,
            AgeTransitionNoticeSentOn = noticeSentOn
        };
        context.Parties.Add(child);
        context.SaveChanges();
        return child.Id;
    }

    private static Guid SeedGuardianEdge(PlatformDbContext context, Guid childId)
    {
        var guardian = Guid.NewGuid();
        context.PartyRelationships.Add(new PartyRelationship
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            FromPartyId = guardian, ToPartyId = childId,
            RelationshipTypeCode = PartyRelationshipTypes.Guardian, IsActive = true
        });
        context.SaveChanges();
        return guardian;
    }

    private static void SeedGrant(PlatformDbContext context, Guid subject, Guid grantedBy, string purpose)
    {
        context.ConsentGrants.Add(new ConsentGrant
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            SubjectPartyId = subject, GrantedByPartyId = grantedBy,
            Purpose = purpose, TermsVersion = "v1", Jurisdiction = "GB",
            VerificationMethod = grantedBy == subject
                ? ConsentVerificationMethods.SelfAuthenticated
                : ConsentVerificationMethods.PaymentInstrument,
            VerifiedAt = Now.AddYears(-5), GrantedAt = Now.AddYears(-5)
        });
        context.SaveChanges();
    }

    // ── Consent age: consents lapse, the edge does NOT ───────────────────

    [Fact]
    public async Task ConsentAge_Should_LapseGuardianConsents_ButKeepTheGuardianEdge()
    {
        await using var context = CreateDbContext();
        var child = SeedChild(context, consentAgeOn: Now.AddDays(-1), majorityOn: Now.AddYears(5));
        var guardian = SeedGuardianEdge(context, child);
        SeedGrant(context, child, guardian, ConsentPurposes.ServiceCore);

        var summary = await CreateService(context).RunAsync();

        summary.ConsentAgeReached.Should().Be(1);

        (await context.ConsentGrants.SingleAsync()).RevokedAt.Should().NotBeNull();
        (await context.ConsentGrants.SingleAsync()).RevocationReason
            .Should().Be(ConsentRevocationReasons.AgeUpLapse);

        // The assertion this whole section exists for. In the UK a 13-year-old self-consents and
        // remains under guardianship until 18 — ending the edge here would strip a parent's
        // authority over their own 14-year-old, five years early.
        (await context.PartyRelationships.SingleAsync()).IsActive.Should().BeTrue(
            "guardianship outlives the consent threshold");

        (await context.Parties.SingleAsync(p => p.Id == child)).ConsentBand
            .Should().Be(PartyConsentBands.SelfConsenting);
    }

    [Fact]
    public async Task ConsentAge_Should_NotLapseASelfGrant()
    {
        await using var context = CreateDbContext();
        var child = SeedChild(context, consentAgeOn: Now.AddDays(-1), majorityOn: Now.AddYears(5));
        SeedGuardianEdge(context, child);
        SeedGrant(context, child, child, ConsentPurposes.ServiceCore); // grantor == subject

        await CreateService(context).RunAsync();

        // Lapsing their own consent on the day they acquire the right to give it would be absurd.
        (await context.ConsentGrants.SingleAsync()).RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task ConsentAge_Should_BeIdempotent()
    {
        await using var context = CreateDbContext();
        var child = SeedChild(context, consentAgeOn: Now.AddDays(-1), majorityOn: Now.AddYears(5));
        var guardian = SeedGuardianEdge(context, child);
        SeedGrant(context, child, guardian, ConsentPurposes.ServiceCore);

        var service = CreateService(context);
        await service.RunAsync();
        var second = await service.RunAsync();

        second.ConsentAgeReached.Should().Be(0,
            "a re-run must find nothing left to do — that is what makes a cron safe");
    }

    // ── Majority: the edge ends ──────────────────────────────────────────

    [Fact]
    public async Task Majority_Should_DeactivateEveryGuardianEdge()
    {
        await using var context = CreateDbContext();
        var child = SeedChild(
            context, consentAgeOn: Now.AddYears(-5), majorityOn: Now.AddDays(-1),
            consentBand: PartyConsentBands.SelfConsenting);
        SeedGuardianEdge(context, child);
        SeedGuardianEdge(context, child);

        var summary = await CreateService(context).RunAsync();

        summary.MajorityReached.Should().Be(1);
        (await context.PartyRelationships.ToListAsync())
            .Should().OnlyContain(r => !r.IsActive, "all guardian authority ends at majority");

        var party = await context.Parties.SingleAsync(p => p.Id == child);
        party.ConsentBand.Should().Be(PartyConsentBands.Adult);
        party.SafetyBand.Should().Be(PartySafetyBands.Adult);
    }

    [Fact]
    public async Task Majority_Should_StageAMajorityReachedEvent()
    {
        await using var context = CreateDbContext();
        var child = SeedChild(
            context, majorityOn: Now.AddDays(-1), consentBand: PartyConsentBands.SelfConsenting);
        var guardian = SeedGuardianEdge(context, child);

        await CreateService(context).RunAsync();

        // The platform says WHAT happened and stages it transactionally; the product decides how
        // anyone is told. Notice copy, the account offer and the suspension experience read very
        // differently for Arke Kids than they would for anything else, so they are not ours.
        var staged = await context.Set<OutboxMessage>()
            .Where(m => m.EventType == typeof(MajorityReachedEvent).FullName)
            .ToListAsync();

        staged.Should().ContainSingle();
        staged[0].Payload.Should().Contain(child.ToString())
            .And.Contain(guardian.ToString(), "the event names who lost authority");
    }

    [Fact]
    public async Task ConsentAge_Should_StageAConsentAgeReachedEvent_NamingTheLapsedPurposes()
    {
        await using var context = CreateDbContext();
        var child = SeedChild(context, consentAgeOn: Now.AddDays(-1), majorityOn: Now.AddYears(5));
        var guardian = SeedGuardianEdge(context, child);
        SeedGrant(context, child, guardian, ConsentPurposes.Voice);

        await CreateService(context).RunAsync();

        var staged = await context.Set<OutboxMessage>()
            .Where(m => m.EventType == typeof(ConsentAgeReachedEvent).FullName)
            .ToListAsync();

        staged.Should().ContainSingle();
        staged[0].Payload.Should().Contain(ConsentPurposes.Voice,
            "the product needs to know what to ask the young person to re-consent to");
    }

    [Fact]
    public async Task Notice_Should_StageAnApproachingEvent_NamingTheTransition()
    {
        await using var context = CreateDbContext();
        SeedChild(context, consentAgeOn: Now.AddDays(10), majorityOn: Now.AddYears(5));

        await CreateService(context).RunAsync();

        var staged = await context.Set<OutboxMessage>()
            .Where(m => m.EventType == typeof(AgeTransitionApproachingEvent).FullName)
            .ToListAsync();

        staged.Should().ContainSingle();
        staged[0].Payload.Should().Contain(AgeTransitionKinds.ConsentAge,
            "consent age and majority are different events with different consequences");
    }

    [Fact]
    public async Task Majority_Should_BeIdempotent()
    {
        await using var context = CreateDbContext();
        var child = SeedChild(
            context, majorityOn: Now.AddDays(-1), consentBand: PartyConsentBands.SelfConsenting);
        SeedGuardianEdge(context, child);

        var service = CreateService(context);
        await service.RunAsync();

        (await service.RunAsync()).MajorityReached.Should().Be(0);
    }

    [Fact]
    public async Task Majority_Should_NotFireBeforeTheDate()
    {
        await using var context = CreateDbContext();
        var child = SeedChild(
            context, majorityOn: Now.AddDays(1), consentBand: PartyConsentBands.SelfConsenting);
        SeedGuardianEdge(context, child);

        (await CreateService(context).RunAsync()).MajorityReached.Should().Be(0);
        (await context.PartyRelationships.SingleAsync()).IsActive.Should().BeTrue();
    }

    // ── Notice ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Notice_Should_BeGivenInAdvance_AndOnlyOnce()
    {
        await using var context = CreateDbContext();
        SeedChild(context, consentAgeOn: Now.AddDays(10), majorityOn: Now.AddYears(5));

        var service = CreateService(context);

        (await service.RunAsync()).NoticesGiven.Should().Be(1);

        // Without the marker a daily cron notifies every day for a month, which is how a
        // considerate feature becomes a nuisance.
        (await service.RunAsync()).NoticesGiven.Should().Be(0);
    }

    [Fact]
    public async Task Notice_Should_NotBeGiven_ForADistantTransition()
    {
        await using var context = CreateDbContext();
        SeedChild(context, consentAgeOn: Now.AddDays(90), majorityOn: Now.AddYears(5));

        (await CreateService(context).RunAsync()).NoticesGiven.Should().Be(0);
    }

    // ── Safety bands ─────────────────────────────────────────────────────

    [Fact]
    public async Task SafetyBand_Should_AdvanceOnItsOwnDate()
    {
        await using var context = CreateDbContext();
        var child = SeedChild(
            context,
            consentAgeOn: Now.AddYears(3),
            majorityOn: Now.AddYears(8),
            safetyBandChangesOn: Now.AddDays(-1),
            safetyBand: PartySafetyBands.Age6To9);

        var summary = await CreateService(context).RunAsync();

        summary.SafetyBandsChanged.Should().Be(1);
        (await context.Parties.SingleAsync(p => p.Id == child)).SafetyBand
            .Should().Be(PartySafetyBands.Age10To12);
    }

    [Fact]
    public async Task SafetyBand_Should_KeepMovingAfterTheConsentAge()
    {
        await using var context = CreateDbContext();
        var child = SeedChild(
            context,
            consentAgeOn: Now.AddDays(-1),
            majorityOn: Now.AddYears(5),
            safetyBandChangesOn: Now.AddDays(-1),
            safetyBand: PartySafetyBands.Age10To12);

        await CreateService(context).RunAsync();

        // Safety banding tracks MINORITY, not consent capacity: acquiring the right to decide about
        // your own data does not stop you being someone these rules protect.
        (await context.Parties.SingleAsync(p => p.Id == child)).SafetyBand
            .Should().Be(PartySafetyBands.Age13ToMajority);
    }

    [Fact]
    public async Task SafetyBand_Should_StopAtTheLastChildBand()
    {
        await using var context = CreateDbContext();
        var child = SeedChild(
            context,
            majorityOn: Now.AddYears(2),
            safetyBandChangesOn: Now.AddDays(-1),
            safetyBand: PartySafetyBands.Age13ToMajority,
            consentBand: PartyConsentBands.SelfConsenting);

        await CreateService(context).RunAsync();

        var party = await context.Parties.SingleAsync(p => p.Id == child);
        party.SafetyBand.Should().Be(PartySafetyBands.Age13ToMajority, "there is no band above it before majority");
        party.SafetyBandChangesOn.Should().BeNull("and nothing further to schedule");
    }

    // ── Tenant scoping ───────────────────────────────────────────────────

    [Fact]
    public async Task FindTenantsWithWork_Should_SeeAcrossTenants()
    {
        await using var context = CreateDbContext();
        SeedChild(context, consentAgeOn: Now.AddDays(-1), majorityOn: Now.AddYears(5));

        // The job needs to know WHERE to go before it can scope itself; every write then happens
        // inside a per-tenant scope, because EnforceTenantOnWrites rejects a cross-tenant save.
        (await CreateService(context).FindTenantsWithWorkAsync())
            .Should().Contain(TenantId);
    }

    [Fact]
    public async Task FindTenantsWithWork_Should_IgnoreAPartyWithNoDates()
    {
        await using var context = CreateDbContext();
        SeedChild(context, consentBand: PartyConsentBands.Adult);

        (await CreateService(context).FindTenantsWithWorkAsync()).Should().BeEmpty(
            "an adult with no scheduled boundary is not work");
    }
}
