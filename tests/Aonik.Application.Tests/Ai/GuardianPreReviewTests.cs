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
using Microsoft.Extensions.Options;

namespace Aonik.Application.Tests.Ai;

/// <summary>
/// Spec 096 §8 — guardian pre-review, and the ordering that keeps it from being a bypass.
///
/// <para>
/// Pre-review holds content the automated layers <em>already allowed</em>. Put the hold before
/// classification instead and guardian approval becomes an unconditional override of the whole gate —
/// the same defect §8's first revision had in giving guardians unconditional release. The test that
/// matters most here is <see cref="BlockedContent_Should_NeverReachThePreReviewQueue"/>, which is the
/// one that would still pass if someone reordered it wrongly and then quietly stop being true.
/// </para>
/// </summary>
public class GuardianPreReviewTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; } = Now;
    }

    private sealed class CleanClassifier : IContentClassifier
    {
        private readonly Dictionary<string, double> _scores;

        public CleanClassifier(Dictionary<string, double>? scores = null)
            => _scores = scores ?? [];

        public string Modality => SafetyModalities.Text;

        public Task<ClassificationResult> ClassifyAsync(
            ClassificationRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ClassificationResult(_scores, Guid.NewGuid()));
    }

    private static AiDbContext CreateDbContext()
        => new(
            new DbContextOptionsBuilder<AiDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                .Options,
            new TestTenantProvider(TenantId),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new TestClock());

    private static GuardianPreReviewService CreateService(
        AiDbContext context, StubGuardianship guardianship)
        => new(context, guardianship, new TestTenantProvider(TenantId), new TestClock(),
            NullLogger<GuardianPreReviewService>.Instance);

    private static ContentSafetyGate CreateGate(
        AiDbContext context, StubGuardianship guardianship, IContentClassifier classifier)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new SafetyOptions());
        return new ContentSafetyGate(
            context,
            new SafetyPolicyReader(context, new TestTenantProvider(TenantId)),
            [classifier],
            new SafetyIncidentRecorder(context, options),
            CreateService(context, guardianship),
            new TestTenantProvider(TenantId),
            new TestClock(),
            options,
            NullLogger<ContentSafetyGate>.Instance);
    }

    // ── The hold ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Under6_Should_BeHeldForGuardianReview_EvenWhenNothingFired()
    {
        await using var context = CreateDbContext();
        var child = Guid.NewGuid();
        var gate = CreateGate(context, new StubGuardianship(), new CleanClassifier());

        var verdict = await gate.ScreenOutputAsync(
            new SafetyRequest(child, SafetyBandNames.Under6, SafetyModalities.Text, Guid.NewGuid()),
            new GeneratedContent(SafetyModalities.Text, "blob://story-1"));

        verdict.Outcome.Should().Be(SafetyDecisionOutcome.HeldForReview);
        verdict.Permit.Should().BeNull("a hold delivers nothing until an adult says so");
        verdict.WasUnavailable.Should().BeFalse("a hold is not an outage and must not page");

        var held = await context.PendingContentReviews.SingleAsync();
        held.SubjectPartyId.Should().Be(child);
        held.SafetyDecisionId.Should().Be(verdict.DecisionId);
        held.State.Should().Be(PreReviewStates.Pending);
    }

    [Fact]
    public async Task HeldContent_Should_NotWriteAnIncident()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubGuardianship(), new CleanClassifier());

        await gate.ScreenOutputAsync(
            new SafetyRequest(Guid.NewGuid(), SafetyBandNames.Under6, SafetyModalities.Text, Guid.NewGuid()),
            new GeneratedContent(SafetyModalities.Text, "blob://story-1"));

        // Nothing was judged unsafe. Filing an incident would put an ordinary held story in the
        // guardian's block list and alarm them about content that passed every check.
        (await context.SafetyIncidents.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task BlockedContent_Should_NeverReachThePreReviewQueue()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(
            context,
            new StubGuardianship(),
            new CleanClassifier(new Dictionary<string, double> { [SafetyCategories.Sexual] = 0.99 }));

        var verdict = await gate.ScreenOutputAsync(
            new SafetyRequest(Guid.NewGuid(), SafetyBandNames.Under6, SafetyModalities.Text, Guid.NewGuid()),
            new GeneratedContent(SafetyModalities.Text, "blob://story-1"));

        // The load-bearing assertion of this file. Pre-review holds what the layers ALLOWED; blocked
        // content goes to the incident path, where the non-overridable branch applies. If a hold could
        // be created from a block, guardian approval would become a release of blocked content and the
        // §8 non-overridable rule would be reachable around rather than through.
        verdict.Outcome.Should().Be(SafetyDecisionOutcome.Blocked);
        (await context.PendingContentReviews.AnyAsync()).Should().BeFalse();
        (await context.SafetyIncidents.AnyAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Age6To9_Should_NotBeHeldByDefault()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubGuardianship(), new CleanClassifier());

        var verdict = await gate.ScreenOutputAsync(
            new SafetyRequest(Guid.NewGuid(), SafetyBandNames.Age6To9, SafetyModalities.Text, Guid.NewGuid()),
            new GeneratedContent(SafetyModalities.Text, "blob://story-1"));

        // Holding every story for an eight-year-old would make the product unusable and train the
        // guardian to approve without looking — which is worse than not holding at all.
        verdict.Allowed.Should().BeTrue();
        verdict.Permit.Should().NotBeNull();
    }

    [Fact]
    public async Task AnUnknownBand_Should_BeHeld()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubGuardianship(), new CleanClassifier());

        var verdict = await gate.ScreenOutputAsync(
            new SafetyRequest(Guid.NewGuid(), "", SafetyModalities.Text, Guid.NewGuid()),
            new GeneratedContent(SafetyModalities.Text, "blob://story-1"));

        verdict.Outcome.Should().Be(SafetyDecisionOutcome.HeldForReview,
            "a band we cannot establish resolves to the youngest, and the youngest is held");
    }

    [Fact]
    public async Task InputScreening_Should_NeverBeHeld()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubGuardianship(), new CleanClassifier());

        var verdict = await gate.ScreenInputAsync(
            new SafetyRequest(Guid.NewGuid(), SafetyBandNames.Under6, SafetyModalities.Text), "a dragon");

        // L2 judges what the child asked for, not what they are about to see. Holding a prompt for
        // parental approval would stop the generation before anything exists to review.
        verdict.Allowed.Should().BeTrue();
        (await context.PendingContentReviews.AnyAsync()).Should().BeFalse();
    }

    // ── Deciding ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Approve_Should_YieldAPermitCarryingTheOriginalDecision()
    {
        await using var context = CreateDbContext();
        var guardian = Guid.NewGuid();
        var child = Guid.NewGuid();
        var guardianship = new StubGuardianship().Add(guardian, child);

        var verdict = await CreateGate(context, guardianship, new CleanClassifier()).ScreenOutputAsync(
            new SafetyRequest(child, SafetyBandNames.Under6, SafetyModalities.Text, Guid.NewGuid()),
            new GeneratedContent(SafetyModalities.Text, "blob://story-1"));

        var held = await context.PendingContentReviews.SingleAsync();
        var decision = await CreateService(context, guardianship).ApproveAsync(guardian, held.Id);

        decision.Outcome.Should().Be(PreReviewOutcome.Approved);
        decision.Permit.Should().NotBeNull();

        // The ORIGINAL decision id, not a fresh one. Nothing was re-judged — the guardian released a
        // verdict the classifiers produced, and delivery stays traceable to the runs that made it.
        decision.Permit!.DecisionId.Should().Be(verdict.DecisionId);
        decision.Permit.SubjectPartyId.Should().Be(child);
    }

    [Fact]
    public async Task Decline_Should_YieldNoPermit()
    {
        await using var context = CreateDbContext();
        var guardian = Guid.NewGuid();
        var child = Guid.NewGuid();
        var guardianship = new StubGuardianship().Add(guardian, child);
        var heldId = await HoldAsync(context, child);

        var decision = await CreateService(context, guardianship).DeclineAsync(guardian, heldId);

        decision.Outcome.Should().Be(PreReviewOutcome.Declined);
        decision.Permit.Should().BeNull();
        (await context.PendingContentReviews.SingleAsync()).State.Should().Be(PreReviewStates.Declined);
    }

    [Fact]
    public async Task ADecidedHold_Should_NotBeDecidedAgain()
    {
        await using var context = CreateDbContext();
        var guardian = Guid.NewGuid();
        var child = Guid.NewGuid();
        var guardianship = new StubGuardianship().Add(guardian, child);
        var heldId = await HoldAsync(context, child);
        var service = CreateService(context, guardianship);

        await service.DeclineAsync(guardian, heldId);

        (await service.ApproveAsync(guardian, heldId)).Outcome
            .Should().Be(PreReviewOutcome.NotAvailable, "a decline is not reversible by re-approving");
    }

    [Fact]
    public async Task AnExpiredHold_Should_ExpireRatherThanApprove()
    {
        await using var context = CreateDbContext();
        var guardian = Guid.NewGuid();
        var child = Guid.NewGuid();
        var guardianship = new StubGuardianship().Add(guardian, child);
        var heldId = await HoldAsync(context, child, expiresAt: Now.AddDays(-1));

        var decision = await CreateService(context, guardianship).ApproveAsync(guardian, heldId);

        // An unattended queue must not become an approval mechanism. Expiry resolves as expiry.
        decision.Outcome.Should().Be(PreReviewOutcome.NotAvailable);
        decision.Permit.Should().BeNull();
        (await context.PendingContentReviews.SingleAsync()).State.Should().Be(PreReviewStates.Expired);
    }

    [Fact]
    public async Task Approve_Should_RequireGuardianAuthority()
    {
        await using var context = CreateDbContext();
        var child = Guid.NewGuid();
        var heldId = await HoldAsync(context, child);

        var act = async () => await CreateService(context, new StubGuardianship())
            .ApproveAsync(Guid.NewGuid(), heldId);

        await act.Should().ThrowAsync<GuardianAuthorityRequiredException>();
    }

    [Fact]
    public async Task ListPending_Should_ExcludeDecidedAndExpiredHolds()
    {
        await using var context = CreateDbContext();
        var guardian = Guid.NewGuid();
        var child = Guid.NewGuid();
        var guardianship = new StubGuardianship().Add(guardian, child);

        var live = await HoldAsync(context, child);
        await HoldAsync(context, child, expiresAt: Now.AddDays(-1));
        var decided = await HoldAsync(context, child);

        var service = CreateService(context, guardianship);
        await service.DeclineAsync(guardian, decided);

        var pending = await service.ListPendingAsync(guardian, child);

        pending.Should().ContainSingle().Which.PendingReviewId.Should().Be(live);
    }

    // ── The preference ───────────────────────────────────────────────────

    [Fact]
    public async Task PreReview_Should_DefaultOn_WhenNoPreferenceRowExists()
    {
        await using var context = CreateDbContext();

        // Absence must not read as "off". A default written as a row at provisioning time means a
        // provisioning bug silently disables pre-review for the band that needs it most.
        (await CreateService(context, new StubGuardianship())
            .RequiresPreReviewAsync(Guid.NewGuid(), SafetyBandNames.Under6))
            .Should().BeTrue();
    }

    [Fact]
    public async Task AGuardian_Should_BeAbleToTurnPreReviewOff_ForTheYoungestBand()
    {
        await using var context = CreateDbContext();
        var guardian = Guid.NewGuid();
        var child = Guid.NewGuid();
        var guardianship = new StubGuardianship().Add(guardian, child);

        await CreateService(context, guardianship).SetPreReviewAsync(guardian, child, enabled: false);

        var verdict = await CreateGate(context, guardianship, new CleanClassifier()).ScreenOutputAsync(
            new SafetyRequest(child, SafetyBandNames.Under6, SafetyModalities.Text, Guid.NewGuid()),
            new GeneratedContent(SafetyModalities.Text, "blob://story-1"));

        // Permitted deliberately: a parent is not a moderation queue, and the automated layers were
        // designed to stand alone. Turning pre-review off weakens none of them.
        verdict.Allowed.Should().BeTrue();
        verdict.Permit.Should().NotBeNull();
    }

    [Fact]
    public async Task AGuardian_Should_BeAbleToTurnPreReviewOn_ForAnOlderBand()
    {
        await using var context = CreateDbContext();
        var guardian = Guid.NewGuid();
        var child = Guid.NewGuid();
        var guardianship = new StubGuardianship().Add(guardian, child);

        await CreateService(context, guardianship).SetPreReviewAsync(guardian, child, enabled: true);

        var verdict = await CreateGate(context, guardianship, new CleanClassifier()).ScreenOutputAsync(
            new SafetyRequest(child, SafetyBandNames.Age10To12, SafetyModalities.Text, Guid.NewGuid()),
            new GeneratedContent(SafetyModalities.Text, "blob://story-1"));

        verdict.Outcome.Should().Be(SafetyDecisionOutcome.HeldForReview,
            "available to any parent who wants it, not only to the youngest band");
    }

    [Fact]
    public async Task SetPreReview_Should_RequireGuardianAuthority()
    {
        await using var context = CreateDbContext();

        var act = async () => await CreateService(context, new StubGuardianship())
            .SetPreReviewAsync(Guid.NewGuid(), Guid.NewGuid(), enabled: false);

        await act.Should().ThrowAsync<GuardianAuthorityRequiredException>();
    }

    [Fact]
    public async Task SetPreReview_Should_ReplaceAnEarlierChoice_RatherThanAccumulate()
    {
        await using var context = CreateDbContext();
        var guardian = Guid.NewGuid();
        var child = Guid.NewGuid();
        var guardianship = new StubGuardianship().Add(guardian, child);
        var service = CreateService(context, guardianship);

        await service.SetPreReviewAsync(guardian, child, enabled: false);
        await service.SetPreReviewAsync(guardian, child, enabled: true);

        context.ChildSafetyPreferences.Should().ContainSingle();
        (await service.RequiresPreReviewAsync(child, SafetyBandNames.Under6)).Should().BeTrue();
    }

    // ── Transparency to the child ────────────────────────────────────────

    [Theory]
    [InlineData(SafetyBandNames.Under6)]
    [InlineData(SafetyBandNames.Age6To9)]
    [InlineData(SafetyBandNames.Age10To12)]
    [InlineData(SafetyBandNames.Age13ToMajority)]
    public async Task TheChild_Should_AlwaysBeToldAnAdultCanSeeTheirWork(string band)
    {
        await using var context = CreateDbContext();
        var guardian = Guid.NewGuid();
        var child = Guid.NewGuid();
        var guardianship = new StubGuardianship().Add(guardian, child);
        var service = CreateService(context, guardianship);

        // Both settings, both directions — there must be no combination that produces covert
        // monitoring. The Children's Code is explicit about this, and a product that lets a parent
        // watch a child secretly is one we should not build regardless.
        foreach (var enabled in new[] { true, false })
        {
            await service.SetPreReviewAsync(guardian, child, enabled);

            var disclosure = await service.DescribeSupervisionAsync(child, band);

            disclosure.GuardianCanSee.Should().BeTrue();
            disclosure.GuardianReviewsBeforeDelivery.Should().Be(enabled);
            disclosure.ChildFacingMessage.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Theory]
    [InlineData(SafetyBandNames.Under6)]
    [InlineData(SafetyBandNames.Age6To9)]
    [InlineData(SafetyBandNames.Age10To12)]
    public async Task TheChildFacingMessage_Should_NotMentionFiltersOrCategories(string band)
    {
        await using var context = CreateDbContext();

        var disclosure = await CreateService(context, new StubGuardianship())
            .DescribeSupervisionAsync(Guid.NewGuid(), band);

        // A seven-year-old told they triggered a "violence filter" learns they did something wrong;
        // they did not.
        foreach (var word in new[] { "filter", "block", "unsafe", "violat", "monitor" })
        {
            disclosure.ChildFacingMessage.Should().NotContainEquivalentOf(word);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static async Task<Guid> HoldAsync(
        AiDbContext context, Guid childPartyId, DateTime? expiresAt = null)
    {
        var review = new PendingContentReview
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            SafetyDecisionId = Guid.NewGuid(),
            SubjectPartyId = childPartyId,
            SafetyBand = SafetyBandNames.Under6,
            Modality = SafetyModalities.Text,
            Reference = "blob://story-1",
            State = PreReviewStates.Pending,
            HeldAt = Now,
            ExpiresAt = expiresAt ?? Now.AddDays(14),
        };

        context.PendingContentReviews.Add(review);
        await context.SaveChangesAsync();
        return review.Id;
    }
}
