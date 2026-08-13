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
/// Spec 096 S5 — voice.
///
/// <para>
/// The phase ships when <strong>generated speech is classified before a child hears it</strong>, a
/// classifier outage blocks narration rather than passing it through, and the delivered-voice path has
/// its own fail-closed test. The assertion that carries the most weight is
/// <see cref="CleanTranscriptWithDistressingDelivery_Should_Block"/>: the transcript passes and the
/// performance does not, which is the exact case a text-only implementation would deliver.
/// </para>
/// </summary>
public class SpeechSafetyTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    /// <summary>
    /// The band the stub reader reports. The gate reads it from the record rather than the request,
    /// so a test that wants a different band sets this instead of passing one in. xUnit builds a new
    /// instance per test, so there is nothing shared here.
    /// </summary>
    private string? _band = SafetyBandNames.Age10To12;

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; } = new(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class StubClassifier : IContentClassifier, ITemporalCoverage
    {
        private readonly Dictionary<string, double> _scores;
        private readonly Exception? _throws;

        public StubClassifier(
            string modality,
            Dictionary<string, double>? scores = null,
            Exception? throws = null,
            TemporalCoverage coverage = TemporalCoverage.Complete)
        {
            Modality = modality;
            _scores = scores ?? [];
            _throws = throws;
            Coverage = coverage;
        }

        public string Modality { get; }

        public TemporalCoverage Coverage { get; }

        public string? LastReference { get; private set; }

        public Task<ClassificationResult> ClassifyAsync(
            ClassificationRequest request, CancellationToken cancellationToken = default)
        {
            LastReference = request.Reference;

            return _throws is not null
                ? Task.FromException<ClassificationResult>(_throws)
                : Task.FromResult(new ClassificationResult(_scores, Guid.NewGuid()));
        }
    }

    private sealed class StubTranscriber : ISpeechTranscriber
    {
        private readonly string _text;
        private readonly Exception? _throws;

        public StubTranscriber(
            string text = "and then the door opened",
            Exception? throws = null,
            TemporalCoverage coverage = TemporalCoverage.Complete,
            string provider = "consented-vendor")
        {
            _text = text;
            _throws = throws;
            Coverage = coverage;
            Provider = provider;
        }

        public TemporalCoverage Coverage { get; }

        public string Provider { get; }

        public string? LastModelName { get; private set; }

        public Task<SpeechTranscript> TranscribeAsync(
            Guid subjectPartyId, string reference, string modelName,
            CancellationToken cancellationToken = default)
        {
            LastModelName = modelName;

            return _throws is not null
                ? Task.FromException<SpeechTranscript>(_throws)
                : Task.FromResult(new SpeechTranscript(_text, Guid.NewGuid()));
        }
    }

    private sealed class StubRouter : ISafetyModelRouter
    {
        private readonly Exception? _throws;

        public StubRouter(Exception? throws = null) => _throws = throws;

        public List<string> ResolvedUseCases { get; } = [];

        public Task<SafetyRoute> ResolveAsync(
            Guid subjectPartyId, string useCase, CancellationToken cancellationToken = default)
        {
            ResolvedUseCases.Add(useCase);

            return _throws is not null
                ? Task.FromException<SafetyRoute>(_throws)
                : Task.FromResult(new SafetyRoute($"model-for-{useCase}", "consented-vendor"));
        }
    }

    private static AiDbContext CreateDbContext()
        => new(
            new DbContextOptionsBuilder<AiDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                .Options,
            new TestTenantProvider(TenantId),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new TestClock());

    private static SpeechContentClassifier CreateSpeechClassifier(
        ISpeechTranscriber? transcriber = null,
        IContentClassifier? transcriptClassifier = null,
        IContentClassifier? audioClassifier = null,
        ISafetyModelRouter? router = null)
        => new(
            transcriber is null ? [] : [transcriber],
            transcriptClassifier, audioClassifier, router ?? new StubRouter(),
            NullLogger<SpeechContentClassifier>.Instance);

    private ContentSafetyGate CreateGate(AiDbContext context, params IContentClassifier[] classifiers)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new SafetyOptions());
        return new ContentSafetyGate(
            context,
            new SafetyPolicyReader(context, new TestTenantProvider(TenantId)),
            classifiers,
            new SafetyIncidentRecorder(context, options, NullLogger<SafetyIncidentRecorder>.Instance),
            new GuardianPreReviewService(
                context, new StubGuardianship(), new TestTenantProvider(TenantId), new TestClock(),
                NullLogger<GuardianPreReviewService>.Instance),
            new StubSafetyBandReader(_band),
            usageMeter: null,
            new TestTenantProvider(TenantId),
            new TestClock(),
            options,
            NullLogger<ContentSafetyGate>.Instance);
    }

    private static ChildNarrationService CreateNarration(ContentSafetyGate gate)
        => new(gate, NullLogger<ChildNarrationService>.Instance);

    private static NarrationRequest ANarration()
        => new(Guid.NewGuid(), "blob://narration-1", Guid.NewGuid());

    private static ClassificationRequest AClassification()
        => new(Guid.NewGuid(), SafetyBandNames.Age6To9, "blob://narration-1");

    // ── Both legs, always ────────────────────────────────────────────────

    [Fact]
    public async Task CleanTranscriptWithDistressingDelivery_Should_Block()
    {
        // The case the whole phase exists for. "And then the door opened" is unremarkable as text and
        // can be delivered in a voice that terrifies a six-year-old. A text-only implementation
        // delivers this; the merge takes the higher score per category, so the audio leg wins.
        var result = await CreateSpeechClassifier(
            new StubTranscriber(),
            new StubClassifier(SafetyModalities.Text),
            new StubClassifier(
                SafetyModalities.Speech,
                new Dictionary<string, double> { [SafetyCategories.Frightening] = 0.93 }))
            .ClassifyAsync(AClassification());

        result.Scores[SafetyCategories.Frightening].Should().Be(0.93);
    }

    [Fact]
    public async Task Merge_Should_TakeTheHigherScore_NotTheAverage()
    {
        var result = await CreateSpeechClassifier(
            new StubTranscriber(),
            new StubClassifier(
                SafetyModalities.Text,
                new Dictionary<string, double> { [SafetyCategories.Frightening] = 0.10 }),
            new StubClassifier(
                SafetyModalities.Speech,
                new Dictionary<string, double> { [SafetyCategories.Frightening] = 0.90 }))
            .ClassifyAsync(AClassification());

        // Averaging would put this at 0.50 and let a clean transcript dilute a distressing delivery
        // below threshold — precisely the case this classifier was built for.
        result.Scores[SafetyCategories.Frightening].Should().Be(0.90);
    }

    [Fact]
    public async Task TheAudioLeg_Should_RunOnTheAudio_NotTheTranscript()
    {
        var audio = new StubClassifier(SafetyModalities.Speech);
        var text = new StubClassifier(SafetyModalities.Text);

        await CreateSpeechClassifier(new StubTranscriber("hello"), text, audio)
            .ClassifyAsync(AClassification());

        text.LastReference.Should().Be("hello", "the transcript leg judges what was said");
        audio.LastReference.Should().Be("blob://narration-1",
            "the audio leg judges how it was said, and a transcript cannot carry that");
    }

    [Fact]
    public async Task EveryRun_Should_BeRecordedOnTheDecision()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, CreateSpeechClassifier(
            new StubTranscriber(), new StubClassifier(SafetyModalities.Text),
            new StubClassifier(SafetyModalities.Speech)));

        await gate.ScreenOutputAsync(
            new SafetyRequest(Guid.NewGuid(), SafetyModalities.Speech, Guid.NewGuid()),
            new GeneratedContent(SafetyModalities.Speech, "blob://narration-1"));

        // Three runs, one verdict. Recording only the first would leave the decision
        // half-reconstructible, which is the failure §15's run ids exist to prevent.
        var decision = await context.SafetyDecisions.SingleAsync();
        decision.ClassifierRunIds!.Split(',').Should().HaveCount(3);
    }

    // ── Fail closed ──────────────────────────────────────────────────────

    [Fact]
    public async Task Narration_Should_BeRefused_WhenNoTranscriberIsConfigured()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, CreateSpeechClassifier(
            transcriber: null,
            transcriptClassifier: new StubClassifier(SafetyModalities.Text),
            audioClassifier: new StubClassifier(SafetyModalities.Speech)));

        var outcome = await CreateNarration(gate).PrepareAsync(ANarration());

        outcome.Narration.Should().BeNull();
        outcome.WasUnavailable.Should().BeTrue();
    }

    [Fact]
    public async Task Narration_Should_BeRefused_WhenOnlyTheTranscriptCanBeClassified()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, CreateSpeechClassifier(
            new StubTranscriber(),
            new StubClassifier(SafetyModalities.Text),
            audioClassifier: null));

        var outcome = await CreateNarration(gate).PrepareAsync(ANarration());

        // Refuses rather than degrading to whichever leg is available. Half-classified narration is
        // not classified narration.
        outcome.Narration.Should().BeNull();
        outcome.WasUnavailable.Should().BeTrue();
    }

    [Fact]
    public async Task Narration_Should_BeRefused_WhenTheTranscriberThrows()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, CreateSpeechClassifier(
            new StubTranscriber(throws: new TimeoutException("stt down")),
            new StubClassifier(SafetyModalities.Text),
            new StubClassifier(SafetyModalities.Speech)));

        var outcome = await CreateNarration(gate).PrepareAsync(ANarration());

        outcome.Narration.Should().BeNull("a classifier outage blocks narration rather than passing it through");
        outcome.WasUnavailable.Should().BeTrue();
    }

    [Fact]
    public async Task Narration_Should_BeRefused_WhenTheAudioClassifierThrows()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, CreateSpeechClassifier(
            new StubTranscriber(),
            new StubClassifier(SafetyModalities.Text),
            new StubClassifier(SafetyModalities.Speech, throws: new HttpRequestException("provider down"))));

        var outcome = await CreateNarration(gate).PrepareAsync(ANarration());

        outcome.Narration.Should().BeNull();
        outcome.WasUnavailable.Should().BeTrue();
    }

    [Fact]
    public async Task Narration_Should_BeRefused_WhenNoSpeechClassifierIsRegisteredAtAll()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubClassifier(SafetyModalities.Text));

        var outcome = await CreateNarration(gate).PrepareAsync(ANarration());

        outcome.Narration.Should().BeNull();
        outcome.WasUnavailable.Should().BeTrue();
    }

    // ── Voice does not inherit coverage ──────────────────────────────────

    [Theory]
    [InlineData(SafetyModalities.Video)]
    [InlineData(SafetyModalities.Image)]
    [InlineData(SafetyModalities.Text)]
    public async Task AnotherModalitysClassifier_Should_NotEnableVoice(string modality)
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new StubClassifier(modality));

        var outcome = await CreateNarration(gate).PrepareAsync(ANarration());

        // "Voice is not enabled by the video phase and does not inherit its coverage." A product that
        // ships video classification and assumes narration is covered has an unclassified path to a
        // child's ears.
        outcome.Narration.Should().BeNull();
        outcome.WasUnavailable.Should().BeTrue();
    }

    // ── The delivered-voice path ─────────────────────────────────────────

    [Fact]
    public async Task CleanNarration_Should_BePlayable()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, CreateSpeechClassifier(
            new StubTranscriber(), new StubClassifier(SafetyModalities.Text),
            new StubClassifier(SafetyModalities.Speech)));

        var outcome = await CreateNarration(gate).PrepareAsync(ANarration());

        outcome.Narration.Should().NotBeNull();
        outcome.Narration!.AudioReference.Should().Be("blob://narration-1");
        outcome.Narration.DecisionId.Should().Be(outcome.DecisionId,
            "playback stays traceable to the verdict that allowed it");
    }

    [Fact]
    public async Task BlockedNarration_Should_NotBePlayable_AndIsNotAnOutage()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, CreateSpeechClassifier(
            new StubTranscriber(),
            new StubClassifier(SafetyModalities.Text),
            new StubClassifier(
                SafetyModalities.Speech,
                new Dictionary<string, double> { [SafetyCategories.Frightening] = 0.95 })));

        var outcome = await CreateNarration(gate).PrepareAsync(ANarration());

        // A family whose narration is silently failing and a family whose story was judged unsafe
        // need different responses, so the two must stay distinguishable.
        outcome.Narration.Should().BeNull();
        outcome.Outcome.Should().Be(SafetyDecisionOutcome.Blocked);
        outcome.WasUnavailable.Should().BeFalse();
        (await context.SafetyIncidents.AnyAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task HeldNarration_Should_NotBePlayable()
    {
        await using var context = CreateDbContext();
        _band = SafetyBandNames.Under6;
        var gate = CreateGate(context, CreateSpeechClassifier(
            new StubTranscriber(), new StubClassifier(SafetyModalities.Text),
            new StubClassifier(SafetyModalities.Speech)));

        var outcome = await CreateNarration(gate).PrepareAsync(ANarration());

        // Pre-review applies to narration exactly as it does to text — a held story is not half-played.
        outcome.Narration.Should().BeNull();
        outcome.Outcome.Should().Be(SafetyDecisionOutcome.HeldForReview);
        outcome.WasUnavailable.Should().BeFalse();
    }

    // ── Codex round 1 ────────────────────────────────────────────────────

    [Fact]
    public async Task Transcription_Should_RouteThroughAiRoutePolicy()
    {
        var router = new StubRouter();
        var transcriber = new StubTranscriber();

        await CreateSpeechClassifier(
            transcriber, new StubClassifier(SafetyModalities.Text),
            new StubClassifier(SafetyModalities.Speech), router)
            .ClassifyAsync(AClassification());

        // Transcription sends a child's audio to a third party, so it cannot be the one call that
        // picks its own vendor and skips the §16.1 consented-provider check.
        router.ResolvedUseCases.Should().Contain(SafetyUseCases.TranscribeSpeech);
        transcriber.LastModelName.Should().Be($"model-for-{SafetyUseCases.TranscribeSpeech}");
    }

    [Fact]
    public async Task AnUnconsentedTranscriptionProvider_Should_RefuseNarration()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, CreateSpeechClassifier(
            new StubTranscriber(), new StubClassifier(SafetyModalities.Text),
            new StubClassifier(SafetyModalities.Speech),
            new StubRouter(throws: new InvalidOperationException("provider not consented"))));

        (await CreateNarration(gate).PrepareAsync(ANarration())).Narration.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyTranscript_Should_RefuseNarration(string transcript)
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, CreateSpeechClassifier(
            new StubTranscriber(transcript), new StubClassifier(SafetyModalities.Text),
            new StubClassifier(SafetyModalities.Speech)));

        var outcome = await CreateNarration(gate).PrepareAsync(ANarration());

        // A successful call returning nothing is a normal failure mode for quiet or unintelligible
        // audio. Treating it as a clean text leg means nobody classified what was said, and the
        // delivery-characteristics leg alone would let the narration through.
        outcome.Narration.Should().BeNull();
        outcome.WasUnavailable.Should().BeTrue();
    }

    [Fact]
    public async Task ANonFiniteScore_Should_RefuseRatherThanOverwriteAUsableOne()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, CreateSpeechClassifier(
            new StubTranscriber(),
            new StubClassifier(
                SafetyModalities.Text,
                new Dictionary<string, double> { [SafetyCategories.Sexual] = 0.99 }),
            new StubClassifier(
                SafetyModalities.Speech,
                new Dictionary<string, double> { [SafetyCategories.Sexual] = double.NaN })));

        var outcome = await CreateNarration(gate).PrepareAsync(ANarration());

        // NaN wins Math.Max and then fails every `>= threshold` comparison, so an unusable audio
        // score could erase a transcript score that was well over the line.
        outcome.Narration.Should().BeNull();
        outcome.WasUnavailable.Should().BeTrue();
    }

    [Fact]
    public async Task AFailedAudioLeg_Should_StillRecordTheRunsThatHappened()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, CreateSpeechClassifier(
            new StubTranscriber(), new StubClassifier(SafetyModalities.Text),
            new StubClassifier(SafetyModalities.Speech, throws: new TimeoutException())));

        await CreateNarration(gate).PrepareAsync(ANarration());

        // Transcription and the transcript leg already produced AiRuns. An outage decision
        // disconnected from AI executions that actually occurred is the audit gap §15 exists to close.
        var decision = await context.SafetyDecisions.SingleAsync();
        decision.Outcome.Should().Be(nameof(SafetyDecisionOutcome.CheckUnavailable));
        decision.ClassifierRunIds!.Split(',').Should().HaveCount(2);
    }

    [Fact]
    public async Task ASamplingTranscriber_Should_RefuseNarration()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, CreateSpeechClassifier(
            new StubTranscriber(coverage: TemporalCoverage.Sampled),
            new StubClassifier(SafetyModalities.Text),
            new StubClassifier(SafetyModalities.Speech)));

        // The composite is only as complete as its least complete leg. Hard-coding completeness here
        // would let a sampling vendor hide behind a wrapper that claims otherwise.
        (await CreateNarration(gate).PrepareAsync(ANarration())).Narration.Should().BeNull();
    }

    [Fact]
    public async Task ASamplingAudioLeg_Should_RefuseNarration()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, CreateSpeechClassifier(
            new StubTranscriber(), new StubClassifier(SafetyModalities.Text),
            new StubClassifier(SafetyModalities.Speech, coverage: TemporalCoverage.Sampled)));

        (await CreateNarration(gate).PrepareAsync(ANarration())).Narration.Should().BeNull();
    }

    [Fact]
    public async Task APermitForOtherAudio_Should_NotProducePlayableNarration()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, CreateSpeechClassifier(
            new StubTranscriber(), new StubClassifier(SafetyModalities.Text),
            new StubClassifier(SafetyModalities.Speech)));

        var permit = (await CreateNarration(gate).PrepareAsync(ANarration())).Narration!.Permit;

        // A permit alone was never enough: any valid one could otherwise be paired with a different
        // reference and unclassified audio laundered through a type that looks checked.
        var construct = () => new PlayableNarration(permit, "blob://something-else");

        construct.Should().Throw<ArgumentException>();
    }

    // ── Codex round 2 ────────────────────────────────────────────────────

    [Fact]
    public async Task Transcription_Should_UseTheAdapterForTheRoutedProvider()
    {
        var routed = new StubTranscriber(provider: "consented-vendor");
        var other = new StubTranscriber(provider: "some-other-vendor");

        var classifier = new SpeechContentClassifier(
            [other, routed],
            new StubClassifier(SafetyModalities.Text),
            new StubClassifier(SafetyModalities.Speech),
            new StubRouter(),
            NullLogger<SpeechContentClassifier>.Instance);

        await classifier.ClassifyAsync(AClassification());

        // The router consent-checks route.Provider. Taking whichever adapter DI happened to hand over
        // would send a child's audio to a company that check never saw.
        routed.LastModelName.Should().NotBeNull();
        other.LastModelName.Should().BeNull();
    }

    [Fact]
    public async Task NoAdapterForTheRoutedProvider_Should_RefuseNarration()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, new SpeechContentClassifier(
            [new StubTranscriber(provider: "some-other-vendor")],
            new StubClassifier(SafetyModalities.Text),
            new StubClassifier(SafetyModalities.Speech),
            new StubRouter(),
            NullLogger<SpeechContentClassifier>.Instance));

        // Refuses rather than substituting. Routing to a provider we have no adapter for is a
        // misconfiguration, and falling back to one we do have defeats the consent check.
        (await CreateNarration(gate).PrepareAsync(ANarration())).Narration.Should().BeNull();
    }

    [Fact]
    public async Task AnInvalidScore_Should_StillRecordTheRunsThatHappened()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, CreateSpeechClassifier(
            new StubTranscriber(),
            new StubClassifier(SafetyModalities.Text),
            new StubClassifier(
                SafetyModalities.Speech,
                new Dictionary<string, double> { [SafetyCategories.Sexual] = double.NaN })));

        await CreateNarration(gate).PrepareAsync(ANarration());

        // All three legs completed before the scores were found unusable, so all three runs exist —
        // and the audit gap does not stop mattering because the failure came from the scores rather
        // than the call.
        var decision = await context.SafetyDecisions.SingleAsync();
        decision.Outcome.Should().Be(nameof(SafetyDecisionOutcome.CheckUnavailable));
        decision.ClassifierRunIds!.Split(',').Should().HaveCount(3);
    }

    [Fact]
    public void PlayableNarration_Should_NotBeConstructibleWithoutAPermit()
    {
        // The permit trick applied one level down. A player that accepts this type cannot be handed
        // unclassified audio, because there is no way to make one without a permit the gate minted.
        var construct = () => new PlayableNarration(null!, "blob://narration-1");

        construct.Should().Throw<ArgumentNullException>();
    }
}
