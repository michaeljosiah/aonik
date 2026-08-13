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
/// Spec 096 S6 — video, and the phase that does not ship.
///
/// <para>
/// F6 is a <strong>product decision</strong> nobody has taken, and the spec is explicit that video
/// staying off is a legitimate outcome rather than a failure. What ships here is therefore not video
/// classification but the two locks that keep it honest when someone comes back to it:
/// </para>
///
/// <list type="number">
///   <item>Video is <strong>off by configuration</strong> — a policy state that refuses without paging,
///   rather than an accident of nobody having registered a classifier.</item>
///   <item>Enabling it is not enough. A temporal classifier must <strong>declare complete
///   coverage</strong>, so "frame sampling alone never satisfies this criterion" is a property of the
///   code rather than a sentence in a document.</item>
/// </list>
/// </summary>
public class VideoSafetyTests
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

    /// <summary>
    /// A classifier that looks at every Nth frame and reports what it saw. Given a video whose single
    /// harmful frame sits <em>between</em> sample points, it truthfully returns clean scores — which is
    /// exactly why the refusal must not depend on it noticing anything.
    /// </summary>
    private sealed class SamplingVideoClassifier : IContentClassifier, ITemporalCoverage
    {
        public string Modality => SafetyModalities.Video;

        public TemporalCoverage Coverage => TemporalCoverage.Sampled;

        public bool WasCalled { get; private set; }

        public Task<ClassificationResult> ClassifyAsync(
            ClassificationRequest request, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(new ClassificationResult(new Dictionary<string, double>(), Guid.NewGuid()));
        }
    }

    /// <summary>A classifier that covers every frame. Hypothetical — no vendor offers this affordably today.</summary>
    private sealed class CompleteVideoClassifier : IContentClassifier, ITemporalCoverage
    {
        private readonly Dictionary<string, double> _scores;

        public CompleteVideoClassifier(Dictionary<string, double>? scores = null) => _scores = scores ?? [];

        public string Modality => SafetyModalities.Video;

        public TemporalCoverage Coverage => TemporalCoverage.Complete;

        public Task<ClassificationResult> ClassifyAsync(
            ClassificationRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ClassificationResult(_scores, Guid.NewGuid()));
    }

    /// <summary>A classifier that has not thought about coverage at all.</summary>
    private sealed class UndeclaredVideoClassifier : IContentClassifier
    {
        public string Modality => SafetyModalities.Video;

        public Task<ClassificationResult> ClassifyAsync(
            ClassificationRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ClassificationResult(new Dictionary<string, double>(), Guid.NewGuid()));
    }

    private static AiDbContext CreateDbContext()
        => new(
            new DbContextOptionsBuilder<AiDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                .Options,
            new TestTenantProvider(TenantId),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new TestClock());

    private ContentSafetyGate CreateGate(
        AiDbContext context, SafetyOptions options, params IContentClassifier[] classifiers)
    {
        var wrapped = Microsoft.Extensions.Options.Options.Create(options);
        return new ContentSafetyGate(
            context,
            new SafetyPolicyReader(context, new TestTenantProvider(TenantId)),
            classifiers,
            new SafetyIncidentRecorder(context, wrapped, NullLogger<SafetyIncidentRecorder>.Instance),
            new GuardianPreReviewService(
                context, new StubGuardianship(), new TestTenantProvider(TenantId), new TestClock(),
                NullLogger<GuardianPreReviewService>.Instance),
            new StubSafetyBandReader(_band),
            usageMeter: null,
            new TestTenantProvider(TenantId),
            new TestClock(),
            wrapped,
            NullLogger<ContentSafetyGate>.Instance);
    }

    private static SafetyOptions WithVideoEnabled()
        => new()
        {
            EnabledModalities =
            [
                SafetyModalities.Text, SafetyModalities.Image,
                SafetyModalities.Speech, SafetyModalities.Video,
            ],
        };

    private static Task<SafetyVerdict> ScreenVideoAsync(ContentSafetyGate gate)
        => gate.ScreenOutputAsync(
            new SafetyRequest(Guid.NewGuid(), SafetyModalities.Video, Guid.NewGuid()),
            new GeneratedContent(SafetyModalities.Video, "blob://clip-1"));

    // ── Lock one: video is off ───────────────────────────────────────────

    [Fact]
    public async Task Video_Should_BeDisabledByDefault()
    {
        await using var context = CreateDbContext();

        var verdict = await ScreenVideoAsync(CreateGate(context, new SafetyOptions()));

        // Not a failure. F6's honest options were complete temporal coverage, short generations,
        // pre-generated and human-reviewed video, or video stays off — and until product picks one,
        // off is the answer the spec sanctions.
        verdict.Allowed.Should().BeFalse();
        verdict.Outcome.Should().Be(SafetyDecisionOutcome.ModalityDisabled);
        verdict.Permit.Should().BeNull();
    }

    [Fact]
    public async Task ADisabledModality_Should_NotLookLikeAnOutage()
    {
        await using var context = CreateDbContext();

        var verdict = await ScreenVideoAsync(CreateGate(context, new SafetyOptions()));

        // A feature nobody turned on cannot be "down". Conflating the two pages an operator forever
        // for video, and teaches them to ignore the alert that matters.
        verdict.WasUnavailable.Should().BeFalse();
        verdict.WasDisabled.Should().BeTrue();
    }

    [Fact]
    public async Task ADisabledModality_Should_StillRecordADecision()
    {
        await using var context = CreateDbContext();

        await ScreenVideoAsync(CreateGate(context, new SafetyOptions()));

        // Every attempted delivery writes a decision record, refusals included — otherwise "why did
        // my child's video never appear?" has no answer at all.
        var decision = await context.SafetyDecisions.SingleAsync();
        decision.Outcome.Should().Be(nameof(SafetyDecisionOutcome.ModalityDisabled));
        decision.Modality.Should().Be(SafetyModalities.Video);
    }

    [Fact]
    public async Task ADisabledModality_Should_NotBeClassified()
    {
        await using var context = CreateDbContext();
        var classifier = new SamplingVideoClassifier();

        await ScreenVideoAsync(CreateGate(context, new SafetyOptions(), classifier));

        // Registering a classifier does not switch a modality on, and a disabled modality does not
        // spend money asking a vendor about content it will refuse regardless.
        classifier.WasCalled.Should().BeFalse();
    }

    [Fact]
    public async Task EnabledModalities_Should_NotIncludeVideo()
    {
        // The default is the shipped decision. If this ever changes, it should be because someone
        // resolved F6 — not because a test was updated to match a config edit.
        new SafetyOptions().ResolvedModalities.Should().NotContain(SafetyModalities.Video);

        // Null-by-default, so an operator narrowing the allowlist REPLACES the defaults instead of
        // adding to them. The configuration binder appends to a pre-populated collection, which would
        // make this allowlist unable to switch anything off.
        new SafetyOptions().EnabledModalities.Should().BeNull();
    }

    // ── Lock two: sampling is not coverage ───────────────────────────────

    [Fact]
    public async Task ASamplingClassifier_Should_BeRefused_EvenWhenItReportsCleanScores()
    {
        await using var context = CreateDbContext();
        var classifier = new SamplingVideoClassifier();

        var verdict = await ScreenVideoAsync(CreateGate(context, WithVideoEnabled(), classifier));

        // The acceptance criterion, demonstrated. This stub models a video whose single harmful frame
        // sits between sample points: every frame it looks at is clean and it says so truthfully. The
        // refusal cannot depend on the classifier noticing — it depends on what it CLAIMS to cover.
        verdict.Allowed.Should().BeFalse("sampling cannot establish that a video is safe");
        verdict.Permit.Should().BeNull();
        classifier.WasCalled.Should().BeFalse("it is refused before it is asked");
    }

    [Fact]
    public async Task ASamplingClassifier_Should_Page()
    {
        await using var context = CreateDbContext();

        var verdict = await ScreenVideoAsync(
            CreateGate(context, WithVideoEnabled(), new SamplingVideoClassifier()));

        // Unlike a disabled modality, this one is somebody's mistake: a design this spec rejects has
        // been deployed, and it needs fixing rather than tolerating.
        verdict.WasUnavailable.Should().BeTrue();
        verdict.WasDisabled.Should().BeFalse();
    }

    [Fact]
    public async Task AClassifierThatDeclaresNothing_Should_BeTreatedAsSampling()
    {
        await using var context = CreateDbContext();

        var verdict = await ScreenVideoAsync(
            CreateGate(context, WithVideoEnabled(), new UndeclaredVideoClassifier()));

        // Silence reads as sampling, not as completeness. A classifier that has not thought about
        // coverage has almost certainly not achieved it.
        verdict.Allowed.Should().BeFalse();
        verdict.WasUnavailable.Should().BeTrue();
    }

    [Fact]
    public async Task ACompleteClassifier_Should_BeAccepted_WhenVideoIsEnabled()
    {
        await using var context = CreateDbContext();

        var verdict = await ScreenVideoAsync(
            CreateGate(context, WithVideoEnabled(), new CompleteVideoClassifier()));

        // The refusal must be narrow: it rejects sampling, not video. When both locks are satisfied
        // the path works, which is what makes shipping it later a configuration change rather than a
        // rewrite.
        verdict.Allowed.Should().BeTrue();
        verdict.Permit.Should().NotBeNull();
    }

    [Fact]
    public async Task ACompleteClassifier_Should_StillBlockHarmfulContent()
    {
        await using var context = CreateDbContext();

        var verdict = await ScreenVideoAsync(CreateGate(
            context,
            WithVideoEnabled(),
            new CompleteVideoClassifier(
                new Dictionary<string, double> { [SafetyCategories.GraphicViolence] = 0.95 })));

        verdict.Outcome.Should().Be(SafetyDecisionOutcome.Blocked,
            "complete coverage is a precondition for judging, not a substitute for it");
    }

    // ── Speech carries the same requirement ──────────────────────────────

    [Fact]
    public void SpeechAndVideo_Should_BothCountAsTemporal()
    {
        // Speech has the same hole for the same reason: a classifier looking at one-second windows
        // misses what falls between them. Listing only video would fix the modality we are not
        // shipping and leave the one we are.
        SafetyModalities.Temporal.Should().BeEquivalentTo(
            [SafetyModalities.Video, SafetyModalities.Speech]);

        SafetyModalities.IsTemporal(SafetyModalities.Image).Should().BeFalse(
            "a still image is either classified or it is not");
    }

    [Fact]
    public async Task ASamplingSpeechClassifier_Should_BeRefused()
    {
        await using var context = CreateDbContext();

        var verdict = await CreateGate(context, new SafetyOptions(), new SamplingSpeechClassifier())
            .ScreenOutputAsync(
                new SafetyRequest(Guid.NewGuid(), SafetyModalities.Speech, Guid.NewGuid()),
                new GeneratedContent(SafetyModalities.Speech, "blob://narration-1"));

        verdict.Allowed.Should().BeFalse();
        verdict.WasUnavailable.Should().BeTrue();
    }

    private sealed class SamplingSpeechClassifier : IContentClassifier, ITemporalCoverage
    {
        public string Modality => SafetyModalities.Speech;

        public TemporalCoverage Coverage => TemporalCoverage.Sampled;

        public Task<ClassificationResult> ClassifyAsync(
            ClassificationRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ClassificationResult(new Dictionary<string, double>(), Guid.NewGuid()));
    }
}
