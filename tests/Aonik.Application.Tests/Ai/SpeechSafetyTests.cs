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

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; } = new(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class StubClassifier : IContentClassifier
    {
        private readonly Dictionary<string, double> _scores;
        private readonly Exception? _throws;

        public StubClassifier(
            string modality, Dictionary<string, double>? scores = null, Exception? throws = null)
        {
            Modality = modality;
            _scores = scores ?? [];
            _throws = throws;
        }

        public string Modality { get; }

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

        public StubTranscriber(string text = "and then the door opened", Exception? throws = null)
        {
            _text = text;
            _throws = throws;
        }

        public Task<SpeechTranscript> TranscribeAsync(
            Guid subjectPartyId, string reference, CancellationToken cancellationToken = default)
            => _throws is not null
                ? Task.FromException<SpeechTranscript>(_throws)
                : Task.FromResult(new SpeechTranscript(_text, Guid.NewGuid()));
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
        IContentClassifier? audioClassifier = null)
        => new(transcriber, transcriptClassifier, audioClassifier,
            NullLogger<SpeechContentClassifier>.Instance);

    private static ContentSafetyGate CreateGate(AiDbContext context, params IContentClassifier[] classifiers)
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
            usageMeter: null,
            new TestTenantProvider(TenantId),
            new TestClock(),
            options,
            NullLogger<ContentSafetyGate>.Instance);
    }

    private static ChildNarrationService CreateNarration(ContentSafetyGate gate)
        => new(gate, NullLogger<ChildNarrationService>.Instance);

    private static NarrationRequest ANarration(string band = SafetyBandNames.Age6To9)
        => new(Guid.NewGuid(), band, "blob://narration-1", Guid.NewGuid());

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
            new SafetyRequest(Guid.NewGuid(), SafetyBandNames.Age6To9, SafetyModalities.Speech, Guid.NewGuid()),
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
        var gate = CreateGate(context, CreateSpeechClassifier(
            new StubTranscriber(), new StubClassifier(SafetyModalities.Text),
            new StubClassifier(SafetyModalities.Speech)));

        var outcome = await CreateNarration(gate).PrepareAsync(ANarration(SafetyBandNames.Under6));

        // Pre-review applies to narration exactly as it does to text — a held story is not half-played.
        outcome.Narration.Should().BeNull();
        outcome.Outcome.Should().Be(SafetyDecisionOutcome.HeldForReview);
        outcome.WasUnavailable.Should().BeFalse();
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
