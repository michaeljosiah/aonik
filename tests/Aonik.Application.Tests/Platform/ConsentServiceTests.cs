using Aonik.Platform.Entities.Party;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Consent;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.SharedKernel.Primitives;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Platform;

/// <summary>
/// Spec 095 G3. The rules with teeth: withdrawal-wins, atomic supersede, enrolment-or-nothing, and
/// the self-consent shape the age-up transition depends on.
/// </summary>
public class ConsentServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; } = Now;
    }

    private sealed class StubMandateReader : IGuardianMandateReader
    {
        private readonly HashSet<Guid> _withMandate;
        public StubMandateReader(params Guid[] withMandate) => _withMandate = [.. withMandate];

        public Task<GuardianMandateInfo?> GetActiveMandateAsync(
            Guid tenantId, Guid partyId, CancellationToken cancellationToken = default)
            => Task.FromResult(_withMandate.Contains(partyId)
                ? new GuardianMandateInfo(Guid.NewGuid(), Now.AddMonths(-3), "Stripe")
                : null);
    }

    private sealed class RecordingVerificationRecorder : IGuardianVerificationRecorder
    {
        public List<(Guid Guardian, bool Succeeded)> Records { get; } = [];

        public Task RecordAsync(
            Guid guardianPartyId, Guid enrolmentAttemptId, GuardianVerificationResult result,
            CancellationToken cancellationToken = default)
        {
            Records.Add((guardianPartyId, result.Succeeded));
            return Task.CompletedTask;
        }

        public Task<int> CountRecentFailuresAsync(
            Guid guardianPartyId, DateTime since, CancellationToken cancellationToken = default)
            => Task.FromResult(Records.Count(r => r.Guardian == guardianPartyId && !r.Succeeded));
    }

    private static PlatformDbContext CreateDbContext()
        => new(
            new DbContextOptionsBuilder<PlatformDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options,
            new TestTenantProvider(TenantId),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new TestClock());

    private static (ConsentService Service, RecordingVerificationRecorder Recorder, GuardianshipReader Guardianship)
        CreateService(PlatformDbContext context, params Guid[] guardiansWithMandate)
    {
        var clock = new TestClock();
        var resolver = new ConsentJurisdictionResolver(
            Microsoft.Extensions.Options.Options.Create(new ConsentOptions()));
        var verifier = new PaymentInstrumentGuardianVerifier(
            new StubMandateReader(guardiansWithMandate),
            NullLogger<PaymentInstrumentGuardianVerifier>.Instance);
        var factory = new GuardianVerifierFactory(new[] { (IGuardianVerifier)verifier });
        var recorder = new RecordingVerificationRecorder();
        var guardianship = new GuardianshipReader(context, clock);

        return (
            new ConsentService(context, new TestTenantProvider(TenantId), clock, resolver, factory, recorder, guardianship),
            recorder,
            guardianship);
    }

    private static Guid SeedParty(PlatformDbContext context, string name = "A Parent")
    {
        var party = new global::Aonik.Platform.Entities.Party.Party
        {
            Id = Guid.NewGuid(), TenantId = TenantId, DisplayName = name,
            PartyType = "Person", Status = "Active"
        };
        context.Parties.Add(party);
        context.SaveChanges();
        return party.Id;
    }

    private static EnrolChildRequest AnEnrolment(Guid guardian, params string[] purposes)
        => new(guardian, "A Child", new DateOnly(2018, 6, 15), "GB", "v1", purposes);

    // ── Enrolment ────────────────────────────────────────────────────────

    [Fact]
    public async Task EnrolChild_Should_CreateChild_GuardianEdge_AndConsents_Together()
    {
        await using var context = CreateDbContext();
        var guardian = SeedParty(context);
        var (service, _, _) = CreateService(context, guardian);

        var result = await service.EnrolChildAsync(AnEnrolment(guardian, ConsentPurposes.SafetyClassification));

        (await context.Parties.CountAsync()).Should().Be(2);
        (await context.PartyRelationships.SingleAsync()).RelationshipTypeCode
            .Should().Be(PartyRelationshipTypes.Guardian);
        (await context.ConsentGrants.CountAsync()).Should().Be(2, "service-core plus the one requested");
        result.ChildPartyId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task EnrolChild_Should_WriteNothing_When_TheGuardianCannotBeVerified()
    {
        await using var context = CreateDbContext();
        var guardian = SeedParty(context);
        var (service, recorder, _) = CreateService(context); // no mandate for anyone

        var act = async () => await service.EnrolChildAsync(AnEnrolment(guardian));

        await act.Should().ThrowAsync<GuardianVerificationFailedException>();

        (await context.Parties.CountAsync()).Should().Be(1, "no child party may be created");
        (await context.PartyRelationships.AnyAsync()).Should().BeFalse();
        (await context.ConsentGrants.AnyAsync()).Should().BeFalse();

        // ...but the ATTEMPT survives. §13 keeps one row per attempt including failures, precisely
        // so repeated failures are visible — rolling that back destroys the signal.
        recorder.Records.Should().ContainSingle(r => r.Guardian == guardian && !r.Succeeded);
    }

    [Fact]
    public async Task EnrolChild_Should_AlwaysIncludeServiceCore_AndNothingElseByDefault()
    {
        await using var context = CreateDbContext();
        var guardian = SeedParty(context);
        var (service, _, _) = CreateService(context, guardian);

        await service.EnrolChildAsync(AnEnrolment(guardian));

        var purposes = await context.ConsentGrants.Select(g => g.Purpose).ToListAsync();
        purposes.Should().BeEquivalentTo(new[] { ConsentPurposes.ServiceCore },
            "everything except service-core defaults to NOT granted");
    }

    [Fact]
    public async Task EnrolChild_Should_ComputeBothBoundaries_AndTheSafetyBand()
    {
        await using var context = CreateDbContext();
        var guardian = SeedParty(context);
        var (service, _, _) = CreateService(context, guardian);

        // Born 2018-06-15; GB consent age 13, majority 18.
        var result = await service.EnrolChildAsync(AnEnrolment(guardian));

        result.ConsentAgeOn.Should().Be(new DateTime(2031, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        result.MajorityOn.Should().Be(new DateTime(2036, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            "guardianship outlives the consent threshold by five years");
        result.SafetyBand.Should().Be(PartySafetyBands.Age6To9, "the child is 8 as of the test clock");
    }

    [Fact]
    public async Task EnrolChild_Should_Refuse_WithoutAnAttestedDate()
    {
        await using var context = CreateDbContext();
        var guardian = SeedParty(context);
        var (service, _, _) = CreateService(context, guardian);

        var act = async () => await service.EnrolChildAsync(
            new EnrolChildRequest(guardian, "A Child", default, "GB", "v1", []));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*no fallback*");
    }

    // ── Multiple guardians, and withdrawal-wins ──────────────────────────

    [Fact]
    public async Task AddGuardian_Should_LetAChildHaveASecondGuardian()
    {
        await using var context = CreateDbContext();
        var first = SeedParty(context, "Parent One");
        var second = SeedParty(context, "Parent Two");
        var (service, _, guardianship) = CreateService(context, first, second);

        var child = (await service.EnrolChildAsync(AnEnrolment(first))).ChildPartyId;
        await service.AddGuardianAsync(new AddGuardianRequest(child, second, first, "GB"));

        (await guardianship.GetGuardiansAsync(TenantId, child))
            .Should().BeEquivalentTo(new[] { first, second },
                "multiplicity is load-bearing — withdrawal-wins means nothing with one guardian");
    }

    [Fact]
    public async Task AddGuardian_Should_Refuse_When_AuthorisedByThemselves()
    {
        await using var context = CreateDbContext();
        var first = SeedParty(context, "Parent One");
        var stranger = SeedParty(context, "Someone Else");
        var (service, _, _) = CreateService(context, first, stranger);

        var child = (await service.EnrolChildAsync(AnEnrolment(first))).ChildPartyId;

        var act = async () => await service.AddGuardianAsync(
            new AddGuardianRequest(child, stranger, stranger, "GB"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot authorise their own addition*");
    }

    [Fact]
    public async Task AddGuardian_Should_Refuse_When_TheAuthoriserIsNotAGuardian()
    {
        await using var context = CreateDbContext();
        var first = SeedParty(context, "Parent One");
        var second = SeedParty(context, "Parent Two");
        var stranger = SeedParty(context, "Someone Else");
        var (service, _, _) = CreateService(context, first, second, stranger);

        var child = (await service.EnrolChildAsync(AnEnrolment(first))).ChildPartyId;

        var act = async () => await service.AddGuardianAsync(
            new AddGuardianRequest(child, second, stranger, "GB"));

        await act.Should().ThrowAsync<GuardianAuthorityRequiredException>();
    }

    [Fact]
    public async Task Withdraw_Should_TakeEffect_EvenWhenAnotherGuardianGranted()
    {
        await using var context = CreateDbContext();
        var granting = SeedParty(context, "Parent One");
        var withdrawing = SeedParty(context, "Parent Two");
        var (service, _, _) = CreateService(context, granting, withdrawing);

        var child = (await service.EnrolChildAsync(
            AnEnrolment(granting, ConsentPurposes.SharingExternal))).ChildPartyId;
        await service.AddGuardianAsync(new AddGuardianRequest(child, withdrawing, granting, "GB"));

        // The withdrawing guardian did not grant this, and the granting guardian has not agreed to
        // withdraw. Withdrawal wins anyway (§7.1) — processing over an objection from someone with
        // legal authority is the worse error, and we do not adjudicate family disputes.
        await service.WithdrawAsync(
            new WithdrawConsentRequest(child, withdrawing, ConsentPurposes.SharingExternal));

        var reader = new ConsentReader(context, new TestClock());
        (await reader.HasConsentAsync(TenantId, child, ConsentPurposes.SharingExternal))
            .Should().BeFalse();
        (await reader.HasConsentAsync(TenantId, child, ConsentPurposes.ServiceCore))
            .Should().BeTrue("withdrawal is per purpose, not wholesale");
    }

    [Fact]
    public async Task Withdraw_Should_Refuse_AStrangerWithNoAuthority()
    {
        await using var context = CreateDbContext();
        var guardian = SeedParty(context);
        var stranger = SeedParty(context, "Someone Else");
        var (service, _, _) = CreateService(context, guardian);

        var child = (await service.EnrolChildAsync(AnEnrolment(guardian))).ChildPartyId;

        var act = async () => await service.WithdrawAsync(
            new WithdrawConsentRequest(child, stranger, ConsentPurposes.ServiceCore));

        await act.Should().ThrowAsync<GuardianAuthorityRequiredException>();
    }

    // ── Terms versioning ─────────────────────────────────────────────────

    [Fact]
    public async Task Grant_Should_AtomicallyRevokeThePriorVersion()
    {
        await using var context = CreateDbContext();
        var guardian = SeedParty(context);
        var (service, _, _) = CreateService(context, guardian);
        var child = (await service.EnrolChildAsync(AnEnrolment(guardian))).ChildPartyId;

        await service.GrantAsync(new GrantConsentRequest(
            child, guardian, ConsentPurposes.ServiceCore, "v2", "GB",
            ConsentVerificationMethods.PaymentInstrument, "ref"));

        var grants = await context.ConsentGrants
            .Where(g => g.Purpose == ConsentPurposes.ServiceCore).ToListAsync();

        grants.Should().HaveCount(2, "the old grant is retained as history, not deleted");
        grants.Count(g => g.RevokedAt == null).Should().Be(1,
            "two active versions must never coexist — the version-agnostic reader would find the stale one");
        grants.Single(g => g.RevokedAt == null).TermsVersion.Should().Be("v2");
        grants.Single(g => g.RevokedAt != null).RevocationReason
            .Should().Be(ConsentRevocationReasons.TermsSuperseded);
    }

    [Fact]
    public async Task PublishTerms_Should_RevokeAtPublication_NotOnReply()
    {
        await using var context = CreateDbContext();
        var guardian = SeedParty(context);
        var (service, _, _) = CreateService(context, guardian);
        var child = (await service.EnrolChildAsync(
            AnEnrolment(guardian, ConsentPurposes.Voice))).ChildPartyId;

        var revoked = await service.PublishTermsVersionAsync(
            new PublishTermsRequest("v2", [ConsentPurposes.ServiceCore]));

        revoked.Should().Be(1);

        var reader = new ConsentReader(context, new TestClock());

        // The guardian has NOT replied. Waiting for them would mean processing under terms this spec
        // calls invalid, indefinitely, for everyone who never answers.
        (await reader.HasConsentAsync(TenantId, child, ConsentPurposes.ServiceCore))
            .Should().BeFalse("publication revokes; it does not wait for a replacement grant");
        (await reader.HasConsentAsync(TenantId, child, ConsentPurposes.Voice))
            .Should().BeTrue("an unaffected purpose keeps its grant");
    }

    // ── Self-consent ─────────────────────────────────────────────────────

    [Fact]
    public async Task Grant_Should_AcceptASelfGrant_WithSelfAuthenticated()
    {
        await using var context = CreateDbContext();
        var subject = SeedParty(context, "A Young Person");
        var (service, _, _) = CreateService(context);

        await service.GrantAsync(new GrantConsentRequest(
            subject, subject, ConsentPurposes.ServiceCore, "v1", "GB",
            ConsentVerificationMethods.SelfAuthenticated, "session-ref"));

        var grant = await context.ConsentGrants.SingleAsync();
        grant.GrantedByPartyId.Should().Be(grant.SubjectPartyId,
            "grantor == subject IS the marker of a self-grant — no separate flag to forget");
    }

    [Fact]
    public async Task Grant_Should_Refuse_SelfAuthenticated_OnAnotherPartysBehalf()
    {
        await using var context = CreateDbContext();
        var guardian = SeedParty(context);
        var child = SeedParty(context, "A Child");
        var (service, _, _) = CreateService(context);

        var act = async () => await service.GrantAsync(new GrantConsentRequest(
            child, guardian, ConsentPurposes.ServiceCore, "v1", "GB",
            ConsentVerificationMethods.SelfAuthenticated, null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be used to consent on another party*");
    }

    [Fact]
    public async Task Grant_Should_Refuse_AParentalMethod_OnASelfGrant()
    {
        await using var context = CreateDbContext();
        var subject = SeedParty(context, "A Young Person");
        var (service, _, _) = CreateService(context);

        var act = async () => await service.GrantAsync(new GrantConsentRequest(
            subject, subject, ConsentPurposes.ServiceCore, "v1", "GB",
            ConsentVerificationMethods.PaymentInstrument, "ref"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must use the self-authenticated method*");
    }

    [Fact]
    public async Task Grant_Should_Refuse_TheLegacyUnverifiedMethod()
    {
        await using var context = CreateDbContext();
        var guardian = SeedParty(context);
        var child = SeedParty(context, "A Child");
        var (service, _, _) = CreateService(context);

        // legacy-unverified is an archive marker. A grant carrying it would authorise on the basis of
        // consent obtained before any verification existed.
        var act = async () => await service.GrantAsync(new GrantConsentRequest(
            child, guardian, ConsentPurposes.ServiceCore, "v1", "GB",
            ConsentVerificationMethods.LegacyUnverified, null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not a valid verification method*");
    }

    // ── Reader behaviour ─────────────────────────────────────────────────

    [Fact]
    public async Task ConsentReader_Should_IgnoreLegacyConsents()
    {
        await using var context = CreateDbContext();
        var child = SeedParty(context, "A Child");

        context.LegacyConsents.Add(new LegacyConsent
        {
            Id = Guid.NewGuid(), TenantId = TenantId, PartyId = child,
            ConsentType = ConsentPurposes.ServiceCore, GrantedAt = Now.AddYears(-2)
        });
        await context.SaveChangesAsync();

        (await new ConsentReader(context, new TestClock())
            .HasConsentAsync(TenantId, child, ConsentPurposes.ServiceCore))
            .Should().BeFalse("the archive authorises nothing — that is why it is a separate table");
    }

    [Fact]
    public async Task GuardianshipReader_Should_RefuseAnEdgePastMajority()
    {
        await using var context = CreateDbContext();
        var guardian = SeedParty(context);

        var adult = new global::Aonik.Platform.Entities.Party.Party
        {
            Id = Guid.NewGuid(), TenantId = TenantId, DisplayName = "Grown Up",
            PartyType = "Person", Status = "Active",
            MajorityOn = Now.AddDays(-1)
        };
        context.Parties.Add(adult);
        context.PartyRelationships.Add(new PartyRelationship
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            FromPartyId = guardian, ToPartyId = adult.Id,
            RelationshipTypeCode = PartyRelationshipTypes.Guardian, IsActive = true
        });
        await context.SaveChangesAsync();

        // The transition job may not have run. The READ must still refuse, or a stale edge is a
        // continuing authority over an adult's own data.
        (await new GuardianshipReader(context, new TestClock())
            .HasAuthorityAsync(TenantId, guardian, adult.Id))
            .Should().BeFalse();
    }
}
