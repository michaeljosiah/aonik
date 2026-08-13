using Aonik.Ai.Entities.Safety;
using Aonik.Ai.Persistence;
using Aonik.Ai.Services.Safety;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.SharedKernel.Abstractions.Safety;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Ai;

/// <summary>
/// Spec 096 §8 — guardian review and the branch no guardian may cross.
///
/// <para>
/// The first revision of this spec said a guardian "can view blocked content and release it",
/// full stop — which handed any guardian account a bypass of the entire safety boundary for sexual
/// content, explicit self-harm and the reporting category. <strong>A guardian account is not proof
/// of good intent</strong>, and the category where that matters most is exactly where an
/// unconditional release capability is most dangerous. These tests are that correction.
/// </para>
/// </summary>
public class GuardianReviewTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; } = Now;
    }

    private static AiDbContext CreateDbContext()
        => new(
            new DbContextOptionsBuilder<AiDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                .Options,
            new TestTenantProvider(TenantId),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new TestClock());

    private static GuardianReviewService CreateService(AiDbContext context, StubGuardianship guardianship)
        => new(context, guardianship, new TestTenantProvider(TenantId), new TestClock(),
            NullLogger<GuardianReviewService>.Instance);

    private static Guid SeedIncident(
        AiDbContext context, Guid childId, string category, bool withArtefact = true)
    {
        var decision = new SafetyDecision
        {
            Id = Guid.NewGuid(), TenantId = TenantId, SubjectPartyId = childId,
            SafetyBand = SafetyBandNames.Age6To9, Modality = SafetyModalities.Text,
            Layer = SafetyLayers.Output, Outcome = nameof(SafetyDecisionOutcome.Blocked),
            SafetyPolicyVersion = "v1", DecidedAt = Now, ExpiresAt = Now.AddDays(90)
        };
        var incident = new SafetyIncident
        {
            Id = Guid.NewGuid(), TenantId = TenantId, SafetyDecisionId = decision.Id,
            SubjectPartyId = childId, Category = category,
            IsNonOverridable = SafetyCategories.IsNonOverridable(category),
            IsUnderLegalHold = SafetyCategories.IsReportable(category),
            AppealState = SafetyAppealStates.None, OccurredAt = Now
        };
        context.SafetyDecisions.Add(decision);
        context.SafetyIncidents.Add(incident);

        if (withArtefact)
        {
            context.SafetyArtefacts.Add(new SafetyArtefact
            {
                Id = Guid.NewGuid(), TenantId = TenantId, SafetyIncidentId = incident.Id,
                Reference = "blob://blocked", ExpiresAt = Now.AddDays(7),
                IsUnderLegalHold = incident.IsUnderLegalHold
            });
        }

        context.SaveChanges();
        return incident.Id;
    }

    // ── The non-overridable branch ───────────────────────────────────────

    [Theory]
    [InlineData(SafetyCategories.Sexual)]
    [InlineData(SafetyCategories.SelfHarm)]
    [InlineData(SafetyCategories.Csam)]
    public async Task Appeal_Should_Refuse_ANonOverridableCategory(string category)
    {
        await using var context = CreateDbContext();
        var guardian = Guid.NewGuid();
        var child = Guid.NewGuid();
        var guardianship = new StubGuardianship();
        guardianship.Add(guardian, child);

        var incidentId = SeedIncident(context, child, category);

        var outcome = await CreateService(context, guardianship).AppealAsync(guardian, incidentId);

        outcome.Outcome.Should().Be(AppealOutcome.Refused,
            "a guardian account is not proof of good intent, and this is where that matters most");

        (await context.SafetyIncidents.SingleAsync()).AppealState
            .Should().Be(SafetyAppealStates.Refused, "the appeal is a signal, and it is recorded");
    }

    [Theory]
    [InlineData(SafetyCategories.Sexual)]
    [InlineData(SafetyCategories.Csam)]
    public async Task NonOverridableIncident_Should_NotBeViewableByTheGuardian(string category)
    {
        await using var context = CreateDbContext();
        var guardian = Guid.NewGuid();
        var child = Guid.NewGuid();
        var guardianship = new StubGuardianship();
        guardianship.Add(guardian, child);
        SeedIncident(context, child, category);

        var listed = await CreateService(context, guardianship).ListForGuardianAsync(guardian, child);

        // Listed, so the guardian is TOLD it happened — telling them nothing would be its own
        // failure. But not viewable: showing them sexual content involving their child would be
        // another.
        listed.Should().ContainSingle();
        listed[0].CanView.Should().BeFalse();
        listed[0].CanRelease.Should().BeFalse();
    }

    // ── The reviewable branch ────────────────────────────────────────────

    [Theory]
    [InlineData(SafetyCategories.GraphicViolence)]
    [InlineData(SafetyCategories.Frightening)]
    public async Task Appeal_Should_Release_AReviewableCategory(string category)
    {
        await using var context = CreateDbContext();
        var guardian = Guid.NewGuid();
        var child = Guid.NewGuid();
        var guardianship = new StubGuardianship();
        guardianship.Add(guardian, child);

        var incidentId = SeedIncident(context, child, category);

        var outcome = await CreateService(context, guardianship).AppealAsync(guardian, incidentId);

        // Where false positives actually live: a knight fighting a dragon is the most common request
        // a six-year-old makes, and a parent's judgement should outrank a threshold there.
        outcome.Outcome.Should().Be(AppealOutcome.Released);
        (await context.SafetyIncidents.SingleAsync()).AppealState
            .Should().Be(SafetyAppealStates.Released);

        // Without a permit, "released" was a status with nothing behind it — the incident said
        // Released and no caller could cross the delivery boundary, so the API reported a release
        // that could not happen.
        outcome.Permit.Should().NotBeNull();
        outcome.Permit!.Authorises(SafetyModalities.Text, "blob://blocked").Should().BeTrue();
    }

    [Fact]
    public async Task ARefusedAppeal_Should_YieldNoPermit()
    {
        await using var context = CreateDbContext();
        var guardian = Guid.NewGuid();
        var child = Guid.NewGuid();
        var guardianship = new StubGuardianship().Add(guardian, child);
        var incidentId = SeedIncident(context, child, SafetyCategories.Csam);

        var outcome = await CreateService(context, guardianship).AppealAsync(guardian, incidentId);

        // The absence is the enforcement, exactly as it is at the gate.
        outcome.Permit.Should().BeNull();
    }

    [Fact]
    public async Task AReleasedPermit_Should_CarryTheOriginalBlockedDecision()
    {
        await using var context = CreateDbContext();
        var guardian = Guid.NewGuid();
        var child = Guid.NewGuid();
        var guardianship = new StubGuardianship().Add(guardian, child);
        var incidentId = SeedIncident(context, child, SafetyCategories.GraphicViolence);

        var outcome = await CreateService(context, guardianship).AppealAsync(guardian, incidentId);

        // Nothing was re-classified: a guardian overrode a verdict, and delivery stays traceable to
        // the verdict they overrode rather than to a fresh one that never happened.
        var incident = await context.SafetyIncidents.AsNoTracking().SingleAsync();
        outcome.Permit!.DecisionId.Should().Be(incident.SafetyDecisionId);
    }

    [Fact]
    public async Task Appeal_Should_BeUnavailable_OnceTheArtefactHasExpired()
    {
        await using var context = CreateDbContext();
        var guardian = Guid.NewGuid();
        var child = Guid.NewGuid();
        var guardianship = new StubGuardianship();
        guardianship.Add(guardian, child);

        var incidentId = SeedIncident(
            context, child, SafetyCategories.GraphicViolence, withArtefact: false);

        // The intended trade: keeping the very thing we judged unsafe, indefinitely, would be
        // perverse, so an appeal window is short and can close.
        (await CreateService(context, guardianship).AppealAsync(guardian, incidentId)).Outcome
            .Should().Be(AppealOutcome.NotAvailable);
    }

    [Fact]
    public async Task Appeal_Should_BeUnavailable_OnceAlreadyDecided()
    {
        await using var context = CreateDbContext();
        var guardian = Guid.NewGuid();
        var child = Guid.NewGuid();
        var guardianship = new StubGuardianship();
        guardianship.Add(guardian, child);

        var incidentId = SeedIncident(context, child, SafetyCategories.GraphicViolence);
        var service = CreateService(context, guardianship);

        await service.AppealAsync(guardian, incidentId);

        (await service.AppealAsync(guardian, incidentId)).Outcome.Should().Be(AppealOutcome.NotAvailable);
    }

    // ── Authority ────────────────────────────────────────────────────────

    [Fact]
    public async Task List_Should_Refuse_APartyWithNoGuardianAuthority()
    {
        await using var context = CreateDbContext();
        var stranger = Guid.NewGuid();
        var child = Guid.NewGuid();
        SeedIncident(context, child, SafetyCategories.GraphicViolence);

        var act = async () => await CreateService(context, new StubGuardianship())
            .ListForGuardianAsync(stranger, child);

        await act.Should().ThrowAsync<GuardianAuthorityRequiredException>();
    }

    [Fact]
    public async Task Appeal_Should_Refuse_AGuardianOfADifferentChild()
    {
        await using var context = CreateDbContext();
        var guardian = Guid.NewGuid();
        var ownChild = Guid.NewGuid();
        var otherChild = Guid.NewGuid();
        var guardianship = new StubGuardianship();
        guardianship.Add(guardian, ownChild);

        var incidentId = SeedIncident(context, otherChild, SafetyCategories.GraphicViolence);

        // Release is to their OWN child only — authority over one child says nothing about another,
        // even within one family.
        var act = async () => await CreateService(context, guardianship)
            .AppealAsync(guardian, incidentId);

        await act.Should().ThrowAsync<GuardianAuthorityRequiredException>();
    }

    [Fact]
    public async Task List_Should_ShowReviewableIncidentsAsViewable()
    {
        await using var context = CreateDbContext();
        var guardian = Guid.NewGuid();
        var child = Guid.NewGuid();
        var guardianship = new StubGuardianship();
        guardianship.Add(guardian, child);
        SeedIncident(context, child, SafetyCategories.GraphicViolence);

        var listed = await CreateService(context, guardianship).ListForGuardianAsync(guardian, child);

        listed[0].CanView.Should().BeTrue();
        listed[0].CanRelease.Should().BeTrue();
    }

    // ── Codex round 1 ────────────────────────────────────────────────────

    [Fact]
    public async Task AViewableIncident_Should_CarryTheArtefactToView()
    {
        await using var context = CreateDbContext();
        var guardian = Guid.NewGuid();
        var child = Guid.NewGuid();
        var guardianship = new StubGuardianship().Add(guardian, child);
        SeedIncident(context, child, SafetyCategories.GraphicViolence);

        var listed = await CreateService(context, guardianship).ListForGuardianAsync(guardian, child);

        // A CanView flag with nothing behind it is a review flow that does not work.
        listed[0].ArtefactReference.Should().Be("blob://blocked");
    }

    [Fact]
    public async Task ANonOverridableIncident_Should_CarryNoArtefactReference()
    {
        await using var context = CreateDbContext();
        var guardian = Guid.NewGuid();
        var child = Guid.NewGuid();
        var guardianship = new StubGuardianship().Add(guardian, child);
        SeedIncident(context, child, SafetyCategories.Csam);

        var listed = await CreateService(context, guardianship).ListForGuardianAsync(guardian, child);

        listed[0].ArtefactReference.Should().BeNull(
            "the reference is the content, and this is the category no guardian may see");
    }

    [Fact]
    public async Task AnExpiredArtefact_Should_LeaveNothingToView()
    {
        await using var context = CreateDbContext();
        var guardian = Guid.NewGuid();
        var child = Guid.NewGuid();
        var guardianship = new StubGuardianship().Add(guardian, child);
        SeedIncident(context, child, SafetyCategories.GraphicViolence, withArtefact: false);

        var listed = await CreateService(context, guardianship).ListForGuardianAsync(guardian, child);

        listed[0].CanView.Should().BeFalse();
        listed[0].ArtefactReference.Should().BeNull();
    }

    [Fact]
    public async Task AnUnknownCategory_Should_NotBeReleasable()
    {
        await using var context = CreateDbContext();
        var guardian = Guid.NewGuid();
        var child = Guid.NewGuid();
        var guardianship = new StubGuardianship().Add(guardian, child);

        // The policy reader blocks unrecognised labels at a low threshold precisely so a classifier
        // that grows a new one does not become silently unenforced. Deciding releasability from set
        // membership alone would undo that here — handing the guardian exactly the categories nobody
        // has classified yet.
        var incidentId = SeedIncident(context, child, "some-new-label");

        var service = CreateService(context, guardianship);

        (await service.ListForGuardianAsync(guardian, child))[0].CanRelease.Should().BeFalse();
        (await service.AppealAsync(guardian, incidentId)).Outcome.Should().Be(AppealOutcome.Refused);
    }

    [Fact]
    public async Task ASecondAppealOnASealedIncident_Should_NotOverwriteTheFirst()
    {
        await using var context = CreateDbContext();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var child = Guid.NewGuid();
        var guardianship = new StubGuardianship().Add(first, child).Add(second, child);

        var incidentId = SeedIncident(context, child, SafetyCategories.Csam);
        var service = CreateService(context, guardianship);

        await service.AppealAsync(first, incidentId);
        (await service.AppealAsync(second, incidentId)).Outcome.Should().Be(AppealOutcome.NotAvailable);

        // Who first reached for a sealed incident is the record the escalation is built on; a retry
        // overwriting it erases exactly the evidence that matters.
        (await context.SafetyIncidents.SingleAsync()).AppealDecidedByPartyId.Should().Be(first);
    }

    [Fact]
    public async Task Escalation_Should_CountAppealsByGuardian_NotByChild()
    {
        await using var context = CreateDbContext();
        var guardian = Guid.NewGuid();
        var guardianship = new StubGuardianship();
        var service = CreateService(context, guardianship);

        // One sealed incident for each of three different wards. Counting by child, this guardian
        // never reaches the threshold — which is the pattern §8 most wants to see.
        const int threshold = 3;

        foreach (var _ in Enumerable.Range(0, threshold))
        {
            var ward = Guid.NewGuid();
            guardianship.Add(guardian, ward);
            await service.AppealAsync(guardian, SeedIncident(context, ward, SafetyCategories.Csam));
        }

        var refusals = await context.SafetyIncidents
            .CountAsync(i => i.AppealDecidedByPartyId == guardian
                && i.AppealState == SafetyAppealStates.Refused);

        refusals.Should().Be(threshold,
            "the identity that matters is the guardian's, not the protected child's");
    }

    [Fact]
    public async Task Appeal_Should_BeUnavailable_ForAnUnknownIncident()
    {
        await using var context = CreateDbContext();

        (await CreateService(context, new StubGuardianship())
            .AppealAsync(Guid.NewGuid(), Guid.NewGuid()))
            .Outcome.Should().Be(AppealOutcome.NotAvailable);
    }
}
