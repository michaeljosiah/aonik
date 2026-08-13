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

        outcome.Should().Be(AppealOutcome.Refused,
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
        outcome.Should().Be(AppealOutcome.Released);
        (await context.SafetyIncidents.SingleAsync()).AppealState
            .Should().Be(SafetyAppealStates.Released);
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
        (await CreateService(context, guardianship).AppealAsync(guardian, incidentId))
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

        (await service.AppealAsync(guardian, incidentId)).Should().Be(AppealOutcome.NotAvailable);
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

    [Fact]
    public async Task Appeal_Should_BeUnavailable_ForAnUnknownIncident()
    {
        await using var context = CreateDbContext();

        (await CreateService(context, new StubGuardianship())
            .AppealAsync(Guid.NewGuid(), Guid.NewGuid()))
            .Should().Be(AppealOutcome.NotAvailable);
    }
}
