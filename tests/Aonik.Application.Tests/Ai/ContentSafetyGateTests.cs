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

    /// <summary>
    /// The band the stub reader reports. The gate reads it from the record rather than the request,
    /// so a test that wants a different band sets this instead of passing one in. xUnit builds a new
    /// instance per test, so there is nothing shared here.
    /// </summary>
    private string? _band = SafetyBandNames.Age10To12;
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

    /// <summary>Stands in for the access-controlled store a reportable input is moved into.</summary>
    private sealed class RecordingPreservedInputStore : IPreservedInputStore
    {
        public List<string> Preserved { get; } = [];

        public Task<string> PreserveAsync(
            Guid subjectPartyId, string input, CancellationToken cancellationToken = default)
        {
            Preserved.Add(input);
            return Task.FromResult($"protected://{Preserved.Count - 1}");
        }
    }

    /// <summary>Captures what actually reaches the logging pipeline.</summary>
    private sealed class CapturingLogger : Microsoft.Extensions.Logging.ILogger<ContentSafetyGate>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }

    private ContentSafetyGate CreateGate(
        AiDbContext context, params IContentClassifier[] classifiers)
        => CreateGate(context, new StubGuardianship(), null, classifiers);

    private ContentSafetyGate CreateGate(
        AiDbContext context, StubGuardianship guardianship, params IContentClassifier[] classifiers)
        => CreateGate(context, guardianship, null, classifiers);

    private ContentSafetyGate CreateGate(
        AiDbContext context,
        StubGuardianship guardianship,
        IPreservedInputStore? preservedInputStore,
        params IContentClassifier[] classifiers)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new SafetyOptions());
        return new ContentSafetyGate(
            context,
            new SafetyPolicyReader(context, new TestTenantProvider(TenantId)),
            classifiers,
            new SafetyIncidentRecorder(context, options, NullLogger<SafetyIncidentRecorder>.Instance),
            new GuardianPreReviewService(
                context, guardianship, new TestTenantProvider(TenantId), new TestClock(),
                NullLogger<GuardianPreReviewService>.Instance),
            new StubSafetyBandReader(_band),
            preservedInputStore,
            usageMeter: null,
            new TestTenantProvider(TenantId),
            new TestClock(),
            options,
            NullLogger<ContentSafetyGate>.Instance);
    }

    private static SafetyRequest ARequest(Guid? runId = null)
        => new(Guid.NewGuid(), SafetyModalities.Text, runId);

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

    // ── The artefact a block leaves behind (Codex round 2) ───────────────

    [Fact]
    public async Task ABlock_Should_PreserveTheContentForGuardianAppeal()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubClassifier(
            scores: new Dictionary<string, double> { [SafetyCategories.GraphicViolence] = 0.99 }));

        await gate.ScreenOutputAsync(ARequest(runId: Guid.NewGuid()), AnOutput());

        // Without this the appeal flow is implemented and inert: every guardian listing reports
        // CanView = false because there is nothing to view, and the retention sweeper has nothing to
        // sweep. Both would look finished and do nothing.
        var incident = await context.SafetyIncidents.SingleAsync();
        var artefact = await context.SafetyArtefacts.SingleAsync();

        artefact.SafetyIncidentId.Should().Be(incident.Id);
        artefact.Reference.Should().Be("blob://story-1");
        artefact.IsUnderLegalHold.Should().BeFalse();
    }

    [Fact]
    public async Task AReportableBlock_Should_PreserveUnderLegalHold()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubClassifier(
            scores: new Dictionary<string, double> { [SafetyCategories.Csam] = 0.99 }));

        await gate.ScreenOutputAsync(ARequest(runId: Guid.NewGuid()), AnOutput());

        // Preservation is automatic on detection and overrides ordinary retention (§12) — it must not
        // depend on someone remembering not to delete.
        (await context.SafetyArtefacts.SingleAsync()).IsUnderLegalHold.Should().BeTrue();
    }

    [Fact]
    public async Task ABlockedPrompt_Should_NotBeStoredAsAnArtefact()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubClassifier(
            scores: new Dictionary<string, double> { [SafetyCategories.SelfHarm] = 0.99 }));

        var verdict = await gate.ScreenInputAsync(ARequest(), new string('x', 3000));

        // At the input layer the "reference" is the child's raw prompt, not a storage key. Writing it
        // into an artefact would break the never-the-content-itself rule AND overflow the column —
        // throwing after the decision was saved, so the gate would never return its fail-closed
        // verdict or release the reservation. §11 wants the prompt un-kept regardless.
        verdict.Allowed.Should().BeFalse();
        (await context.SafetyArtefacts.AnyAsync()).Should().BeFalse();
        (await context.SafetyIncidents.AnyAsync()).Should().BeTrue(
            "the incident still exists — it is the content pointer that must not");
    }

    [Fact]
    public async Task AReportableInput_Should_BePreservedThroughTheProtectedStore()
    {
        await using var context = CreateDbContext();
        var store = new RecordingPreservedInputStore();
        var gate = CreateGate(
            context,
            new StubGuardianship(),
            store,
            new StubClassifier(scores: new Dictionary<string, double> { [SafetyCategories.Csam] = 0.99 }));

        await gate.ScreenInputAsync(ARequest(), "the prompt");

        // The one case where discarding an input is the worse error. Not stored inline — the prompt
        // goes to an access-controlled store and only the key is kept.
        store.Preserved.Should().ContainSingle().Which.Should().Be("the prompt");

        var artefact = await context.SafetyArtefacts.SingleAsync();
        artefact.Reference.Should().Be("protected://0");
        artefact.IsUnderLegalHold.Should().BeTrue();

        (await context.SafetyEscalations.SingleAsync()).MaterialPreserved.Should().BeTrue();
    }

    [Fact]
    public async Task AReportableInput_WithNoStore_Should_RecordThatNothingWasPreserved()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubClassifier(
            scores: new Dictionary<string, double> { [SafetyCategories.Csam] = 0.99 }));

        await gate.ScreenInputAsync(ARequest(), "the prompt");

        // Not silently dropped. The escalation says plainly that nothing was preserved, so the
        // responsible person knows they are acting on a record with nothing behind it.
        (await context.SafetyArtefacts.AnyAsync()).Should().BeFalse();

        // False, not null. Preservation was required, was attempted, and did not happen — the state a
        // custodian must see at a glance rather than infer from a missing artefact.
        (await context.SafetyEscalations.SingleAsync()).MaterialPreserved.Should().BeFalse();
    }

    [Fact]
    public async Task AnOrphanedPreservedKey_Should_NotReachTheLogs()
    {
        await using var context = CreateDbContext();
        var logger = new CapturingLogger();
        var options = Microsoft.Extensions.Options.Options.Create(new SafetyOptions());

        // Preservation succeeds, then linking it fails.
        var gate = new ContentSafetyGate(
            context,
            new SafetyPolicyReader(context, new TestTenantProvider(TenantId)),
            [new StubClassifier(scores: new Dictionary<string, double> { [SafetyCategories.Csam] = 0.99 })],
            new FailingAttachRecorder(context, options),
            new GuardianPreReviewService(
                context, new StubGuardianship(), new TestTenantProvider(TenantId), new TestClock(),
                NullLogger<GuardianPreReviewService>.Instance),
            new StubSafetyBandReader(_band),
            new RecordingPreservedInputStore(),
            usageMeter: null,
            new TestTenantProvider(TenantId),
            new TestClock(),
            options,
            logger);

        await gate.ScreenInputAsync(ARequest(), "the prompt");

        // PreservedMaterialService releases this reference only after the named-custodian check and
        // records every attempt. Emitting it here would put the key in the ordinary logging pipeline,
        // where anyone with log access obtains it outside both controls — worse than the orphan.
        logger.Messages.Should().NotBeEmpty();
        logger.Messages.Should().NotContain(m => m.Contains("protected://", StringComparison.Ordinal));
    }

    /// <summary>Records the incident, then fails to link the preserved material.</summary>
    private sealed class FailingAttachRecorder : ISafetyIncidentRecorder
    {
        private readonly SafetyIncidentRecorder _inner;

        public FailingAttachRecorder(
            AiDbContext context, Microsoft.Extensions.Options.IOptions<SafetyOptions> options)
            => _inner = new SafetyIncidentRecorder(
                context, options, NullLogger<SafetyIncidentRecorder>.Instance);

        public Task<Guid> RecordAsync(
            SafetyDecisionRecord record, CancellationToken cancellationToken = default)
            => _inner.RecordAsync(record, cancellationToken);

        public Task<Guid> RecordIncidentAsync(
            Guid decisionId, Guid subjectPartyId, string category, string contentReference,
            DateTime occurredAt, CancellationToken cancellationToken = default)
            => _inner.RecordIncidentAsync(
                decisionId, subjectPartyId, category, contentReference, occurredAt, cancellationToken);

        public Task AttachPreservedMaterialAsync(
            Guid incidentId, string reference, DateTime occurredAt,
            CancellationToken cancellationToken = default)
            => Task.FromException(new InvalidOperationException("database unavailable"));

        public Task MarkPreservationFailedAsync(
            Guid incidentId, CancellationToken cancellationToken = default)
            => _inner.MarkPreservationFailedAsync(incidentId, cancellationToken);
    }

    [Fact]
    public async Task AnOrdinaryInputBlock_Should_NotReachTheProtectedStore()
    {
        await using var context = CreateDbContext();
        var store = new RecordingPreservedInputStore();
        var gate = CreateGate(
            context,
            new StubGuardianship(),
            store,
            new StubClassifier(
                scores: new Dictionary<string, double> { [SafetyCategories.GraphicViolence] = 0.99 }));

        await gate.ScreenInputAsync(ARequest(), "a knight fights a dragon");

        // §11: a child's own input is not material we keep. Only the reportable category inverts that.
        store.Preserved.Should().BeEmpty();
        (await context.SafetyArtefacts.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task AFailureResolvingPreReview_Should_StillRecordADecisionAndRelease()
    {
        await using var context = CreateDbContext();
        var meter = new ThrowingPreReviewHarness.RecordingMeter();
        var gate = ThrowingPreReviewHarness.CreateGate(context, TenantId, meter);

        var reservationId = Guid.NewGuid();
        var verdict = await gate.ScreenOutputAsync(
            new SafetyRequest(Guid.NewGuid(), SafetyModalities.Text, Guid.NewGuid(), reservationId),
            AnOutput());

        // Pre-review resolves BEFORE the decision is recorded, so a failure there would otherwise
        // leave a completed classifier run with no decision linking it to the attempted delivery —
        // and no reservation released.
        verdict.WasUnavailable.Should().BeTrue();
        verdict.Permit.Should().BeNull();
        (await context.SafetyDecisions.SingleAsync()).Outcome
            .Should().Be(nameof(SafetyDecisionOutcome.CheckUnavailable));
        meter.Released.Should().ContainSingle().Which.Should().Be(reservationId);
    }

    [Fact]
    public async Task AnUnavailableCheck_Should_PreserveNothing()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubClassifier(throws: new TimeoutException()));

        await gate.ScreenOutputAsync(ARequest(runId: Guid.NewGuid()), AnOutput());

        // Nothing was judged, so there is no verdict to appeal and no reason to keep the content.
        // Preserving it would be retention without a purpose.
        (await context.SafetyArtefacts.AnyAsync()).Should().BeFalse();
    }

    // ── Bands and thresholds ─────────────────────────────────────────────

    [Fact]
    public async Task UnknownBand_Should_BeTreatedAsTheStrictest()
    {
        await using var context = CreateDbContext();

        // No band on the party record — the case a caller could previously paper over by asserting
        // one in the request.
        _band = null;
        var gate = CreateGate(context, new StubClassifier(
            scores: new Dictionary<string, double> { [SafetyCategories.Frightening] = 0.45 }));

        var verdict = await gate.ScreenOutputAsync(
            new SafetyRequest(Guid.NewGuid(), SafetyModalities.Text, Guid.NewGuid()), AnOutput());

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
