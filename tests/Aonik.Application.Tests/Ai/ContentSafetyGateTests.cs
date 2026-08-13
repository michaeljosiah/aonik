using Aonik.Ai.Entities.Safety;
using Aonik.Ai.Persistence;
using Aonik.Ai.Services.Safety;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Safety;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Ai;

/// <summary>
/// Spec 096 S1 — the safety gate.
///
/// <para>
/// Every test here is shaped by one asymmetry: the failure is not statistical. A 0.1% error rate on
/// invoice categorisation is a good system; <strong>one frightening image reaching one seven-year-old
/// is a complete failure</strong>. So the interesting assertions are all about refusal — and
/// particularly about refusing when the check itself could not be performed, which is the case a
/// naïve implementation lets through.
/// </para>
/// </summary>
public class ContentSafetyGateTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; } = Now;
    }

    private sealed class StubClassifier : IContentClassifier
    {
        private readonly Dictionary<string, double> _scores;
        private readonly Exception? _throws;

        public StubClassifier(
            string modality = SafetyModalities.Text,
            Dictionary<string, double>? scores = null,
            Exception? throws = null)
        {
            Modality = modality;
            _scores = scores ?? new Dictionary<string, double>();
            _throws = throws;
        }

        public string Modality { get; }

        public Task<ClassificationResult> ClassifyAsync(
            ClassificationRequest request, CancellationToken cancellationToken = default)
            => _throws is not null
                ? Task.FromException<ClassificationResult>(_throws)
                : Task.FromResult(new ClassificationResult(_scores, Guid.NewGuid()));
    }

    private static AiDbContext CreateDbContext()
        => new(
            new DbContextOptionsBuilder<AiDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                .Options,
            new TestTenantProvider(TenantId),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new TestClock());

    private static ContentSafetyGate CreateGate(
        AiDbContext context, params IContentClassifier[] classifiers)
        => CreateGate(context, new StubGuardianship(), classifiers);

    private static ContentSafetyGate CreateGate(
        AiDbContext context, StubGuardianship guardianship, params IContentClassifier[] classifiers)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new SafetyOptions());
        return new ContentSafetyGate(
            context,
            new SafetyPolicyReader(context, new TestTenantProvider(TenantId)),
            classifiers,
            new SafetyIncidentRecorder(context, options),
            new GuardianPreReviewService(
                context, guardianship, new TestTenantProvider(TenantId), new TestClock(),
                NullLogger<GuardianPreReviewService>.Instance),
            new TestTenantProvider(TenantId),
            new TestClock(),
            options,
            NullLogger<ContentSafetyGate>.Instance);
    }

    private static SafetyRequest ARequest(string band = "6-9", Guid? runId = null)
        => new(Guid.NewGuid(), band, SafetyModalities.Text, runId);

    private static GeneratedContent AnOutput() => new(SafetyModalities.Text, "blob://story-1");

    // ── Fail closed ──────────────────────────────────────────────────────

    [Fact]
    public async Task Output_Should_Refuse_WhenTheClassifierThrows()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubClassifier(throws: new TimeoutException("provider down")));

        var verdict = await gate.ScreenOutputAsync(ARequest(runId: Guid.NewGuid()), AnOutput());

        // The single most important behaviour in this file. A classifier that returns "safe" on
        // error is the worst defect available here, and this is the case that produces it.
        verdict.Allowed.Should().BeFalse();
        verdict.WasUnavailable.Should().BeTrue("an unavailable check is an outage, not a pass");
        verdict.Permit.Should().BeNull("nothing may be delivered without a verdict that allowed it");
    }

    [Fact]
    public async Task Output_Should_Refuse_WhenNoClassifierIsRegisteredForTheModality()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubClassifier(SafetyModalities.Text));

        // Image, not video: video is disabled outright (S6), which is a different case with a
        // different outcome. This one is about an ENABLED modality nothing can judge.
        var verdict = await gate.ScreenOutputAsync(
            ARequest(runId: Guid.NewGuid()), new GeneratedContent(SafetyModalities.Image, "blob://i"));

        verdict.Allowed.Should().BeFalse(
            "a missing classifier is an unavailable feature, not an unchecked one");
        verdict.WasUnavailable.Should().BeTrue();
    }

    [Fact]
    public async Task Output_Should_RecordADecision_EvenWhenTheCheckWasUnavailable()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubClassifier(throws: new InvalidOperationException()));

        await gate.ScreenOutputAsync(ARequest(runId: Guid.NewGuid()), AnOutput());

        var decision = await context.SafetyDecisions.SingleAsync();
        decision.Outcome.Should().Be(nameof(SafetyDecisionOutcome.CheckUnavailable));
    }

    // ── The permit is the enforcement ────────────────────────────────────

    [Fact]
    public async Task Output_Should_IssueAPermit_OnlyWhenAllowed()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubClassifier());

        var verdict = await gate.ScreenOutputAsync(ARequest(runId: Guid.NewGuid()), AnOutput());

        verdict.Allowed.Should().BeTrue();
        verdict.Permit.Should().NotBeNull();
        verdict.Permit!.DecisionId.Should().Be(verdict.DecisionId,
            "delivery must be traceable to the verdict that authorised it");
    }

    [Fact]
    public async Task Output_Should_NotIssueAPermit_WhenBlocked()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubClassifier(
            scores: new Dictionary<string, double> { [SafetyCategories.GraphicViolence] = 0.9 }));

        var verdict = await gate.ScreenOutputAsync(ARequest(runId: Guid.NewGuid()), AnOutput());

        verdict.Allowed.Should().BeFalse();
        verdict.Permit.Should().BeNull();
    }

    [Fact]
    public async Task Input_Should_NeverIssueAPermit()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubClassifier());

        var verdict = await gate.ScreenInputAsync(ARequest(), "a knight fights a dragon");

        verdict.Allowed.Should().BeTrue();
        verdict.Permit.Should().BeNull(
            "screening an input authorises dispatch, never delivery — only L4 can permit that");
    }

    // ── Input blocks have no generation run ──────────────────────────────

    [Fact]
    public async Task InputBlock_Should_RecordANullGenerationRun()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubClassifier(
            scores: new Dictionary<string, double> { [SafetyCategories.Sexual] = 0.9 }));

        // L2 runs BEFORE dispatch — that is the point of it being the cheap layer — so no generation
        // run exists. A non-null column would force an implementation to fabricate an AI execution
        // that never happened in order to log the block correctly.
        await gate.ScreenInputAsync(ARequest(runId: Guid.NewGuid()), "…");

        var decision = await context.SafetyDecisions.SingleAsync();
        decision.GenerationRunId.Should().BeNull();
        decision.Layer.Should().Be(SafetyLayers.Input);
        decision.ClassifierRunIds.Should().NotBeNullOrWhiteSpace("the classifier run is still required");
    }

    // ── Every attempt is recorded, allowed included ──────────────────────

    [Fact]
    public async Task Allowed_Should_StillRecordADecisionWithItsRuns()
    {
        await using var context = CreateDbContext();
        var generationRun = Guid.NewGuid();
        var gate = CreateGate(context, new StubClassifier());

        await gate.ScreenOutputAsync(ARequest(runId: generationRun), AnOutput());

        // A delivery later identified as a false negative must be reconstructible. An audit trail
        // that only covers blocks covers everything except the failure it is for.
        var decision = await context.SafetyDecisions.SingleAsync();
        decision.Outcome.Should().Be(nameof(SafetyDecisionOutcome.Allowed));
        decision.GenerationRunId.Should().Be(generationRun);
        decision.ClassifierRunIds.Should().NotBeNullOrWhiteSpace();
        decision.SafetyPolicyVersion.Should().NotBeNullOrWhiteSpace(
            "otherwise a reviewer must infer the threshold from timestamps");
    }

    [Fact]
    public async Task Allowed_Should_NotWriteAnIncident()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubClassifier());

        await gate.ScreenOutputAsync(ARequest(runId: Guid.NewGuid()), AnOutput());

        (await context.SafetyIncidents.AnyAsync()).Should().BeFalse(
            "an incident is the BLOCKED subset of decisions");
    }

    // ── Blocking, categories and severity ────────────────────────────────

    [Fact]
    public async Task Block_Should_WriteAnIncidentUnderTheMostSevereCategory()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubClassifier(scores: new Dictionary<string, double>
        {
            [SafetyCategories.Frightening] = 0.99,
            [SafetyCategories.Sexual] = 0.9,
        }));

        await gate.ScreenOutputAsync(ARequest(runId: Guid.NewGuid()), AnOutput());

        // Ordered by CONSEQUENCE, not by score. A reportable or non-overridable category must win
        // even at lower confidence, because the response it triggers is not a moderation decision.
        var incident = await context.SafetyIncidents.SingleAsync();
        incident.Category.Should().Be(SafetyCategories.Sexual);
        incident.IsNonOverridable.Should().BeTrue("no guardian may release sexual content");
    }

    [Fact]
    public async Task Block_Should_SetALegalHold_ForAReportableCategory()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubClassifier(
            scores: new Dictionary<string, double> { [SafetyCategories.Csam] = 0.9 }));

        await gate.ScreenOutputAsync(ARequest(runId: Guid.NewGuid()), AnOutput());

        var incident = await context.SafetyIncidents.SingleAsync();
        incident.IsUnderLegalHold.Should().BeTrue(
            "preservation overrides ordinary retention and any deletion request");
        incident.IsNonOverridable.Should().BeTrue();
    }

    [Fact]
    public async Task Block_Should_NotMarkAReviewableCategoryAsNonOverridable()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubClassifier(
            scores: new Dictionary<string, double> { [SafetyCategories.GraphicViolence] = 0.9 }));

        await gate.ScreenOutputAsync(ARequest(runId: Guid.NewGuid()), AnOutput());

        // Where false positives actually live: a knight fighting a dragon is the most common request
        // a six-year-old makes, and a parent's judgement should outrank a threshold there.
        var incident = await context.SafetyIncidents.SingleAsync();
        incident.IsNonOverridable.Should().BeFalse();
        incident.IsUnderLegalHold.Should().BeFalse();
    }

    // ── Bands and thresholds ─────────────────────────────────────────────

    [Fact]
    public async Task UnknownBand_Should_BeTreatedAsTheStrictest()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubClassifier(
            scores: new Dictionary<string, double> { [SafetyCategories.Frightening] = 0.45 }));

        var verdict = await gate.ScreenOutputAsync(
            new SafetyRequest(Guid.NewGuid(), "", SafetyModalities.Text, Guid.NewGuid()), AnOutput());

        verdict.Allowed.Should().BeFalse();

        var decision = await context.SafetyDecisions.SingleAsync();
        decision.SafetyBand.Should().Be("under-6",
            "a party whose age we cannot establish is treated as the youngest, not as an adult");
    }

    [Fact]
    public async Task UnknownCategory_Should_BlockRatherThanPass()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubClassifier(
            scores: new Dictionary<string, double> { ["some-new-label"] = 0.8 }));

        var verdict = await gate.ScreenOutputAsync(ARequest(runId: Guid.NewGuid()), AnOutput());

        verdict.Allowed.Should().BeFalse(
            "a classifier that grows a new label must not become silently unenforced");
    }

    [Fact]
    public async Task ScoreBelowThreshold_Should_NotBlock()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubClassifier(
            scores: new Dictionary<string, double> { [SafetyCategories.Frightening] = 0.05 }));

        (await gate.ScreenOutputAsync(ARequest(runId: Guid.NewGuid()), AnOutput()))
            .Allowed.Should().BeTrue("the refusal must be narrow, or ordinary storytelling breaks");
    }

    [Fact]
    public async Task UnconfiguredTenant_Should_UseTheStrictBuiltInPolicy()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubClassifier(
            scores: new Dictionary<string, double> { [SafetyCategories.Csam] = 0.02 }));

        var verdict = await gate.ScreenOutputAsync(ARequest(runId: Guid.NewGuid()), AnOutput());

        verdict.Allowed.Should().BeFalse(
            "an unconfigured tenant must be safe, not unguarded — S0 tunes the numbers later");
    }
}
