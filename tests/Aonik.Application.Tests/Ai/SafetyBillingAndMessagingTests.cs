using Aonik.Ai.Persistence;
using Aonik.Ai.Services.Safety;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Safety;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Ai;

/// <summary>
/// Spec 096 §10.1, §10.2, §18.6, §18.7, §18.28 — what the family is charged, and what the child reads.
///
/// <para>
/// Both are places where a safety system quietly becomes user-hostile. Charging for content we refused
/// to show is indefensible; telling a seven-year-old they broke a rule teaches them they did something
/// wrong when they did not.
/// </para>
/// </summary>
public class SafetyBillingAndMessagingTests
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

    private sealed class StubClassifier : IContentClassifier
    {
        private readonly Dictionary<string, double> _scores;
        private readonly Exception? _throws;

        public StubClassifier(Dictionary<string, double>? scores = null, Exception? throws = null)
        {
            _scores = scores ?? [];
            _throws = throws;
        }

        public string Modality => SafetyModalities.Text;

        public Task<ClassificationResult> ClassifyAsync(
            ClassificationRequest request, CancellationToken cancellationToken = default)
            => _throws is not null
                ? Task.FromException<ClassificationResult>(_throws)
                : Task.FromResult(new ClassificationResult(_scores, Guid.NewGuid()));
    }

    private sealed class RecordingMeter : IUsageMeter
    {
        private readonly Exception? _releaseThrows;

        public RecordingMeter(Exception? releaseThrows = null) => _releaseThrows = releaseThrows;

        public List<Guid> Released { get; } = [];

        public Task ReleaseAsync(Guid reservationId, CancellationToken cancellationToken = default)
        {
            Released.Add(reservationId);
            return _releaseThrows is not null ? Task.FromException(_releaseThrows) : Task.CompletedTask;
        }

        public Task<UsageReservationRef> ReserveAsync(
            SubscriberRef subscriber, string meterCode, decimal quantity, string idempotencyKey,
            TimeSpan? holdFor = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<UsageCommitResult> CommitAsync(
            Guid reservationId, decimal actualQuantity, UsageSource source,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task ClaimSlotAsync(
            SubscriberRef subscriber, string meterCode, string holderRef,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task ReleaseSlotAsync(
            SubscriberRef subscriber, string meterCode, string holderRef,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> HasFlagAsync(
            SubscriberRef subscriber, string meterCode, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
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
        AiDbContext context, IUsageMeter? meter, params IContentClassifier[] classifiers)
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
            meter,
            new TestTenantProvider(TenantId),
            new TestClock(),
            options,
            NullLogger<ContentSafetyGate>.Instance);
    }

    private static SafetyRequest ARequest(Guid reservationId, string band = SafetyBandNames.Age6To9)
        => new(Guid.NewGuid(), SafetyModalities.Text, Guid.NewGuid(), reservationId);

    private static GeneratedContent AnOutput() => new(SafetyModalities.Text, "blob://story-1");

    // ── Nobody is billed for content they were not shown ─────────────────

    [Fact]
    public async Task ABlockedGeneration_Should_ReleaseItsReservation()
    {
        await using var context = CreateDbContext();
        var meter = new RecordingMeter();
        var reservationId = Guid.NewGuid();
        var gate = CreateGate(context, meter, new StubClassifier(
            new Dictionary<string, double> { [SafetyCategories.Sexual] = 0.99 }));

        await gate.ScreenOutputAsync(ARequest(reservationId), AnOutput());

        // §10.1: charging a family a story credit for content we refused to show is indefensible.
        meter.Released.Should().ContainSingle().Which.Should().Be(reservationId);
    }

    [Fact]
    public async Task AFailedCheck_Should_ReleaseItsReservation()
    {
        await using var context = CreateDbContext();
        var meter = new RecordingMeter();
        var reservationId = Guid.NewGuid();
        var gate = CreateGate(context, meter, new StubClassifier(throws: new TimeoutException()));

        await gate.ScreenOutputAsync(ARequest(reservationId), AnOutput());

        // Our outage, our cost.
        meter.Released.Should().ContainSingle().Which.Should().Be(reservationId);
    }

    [Fact]
    public async Task AHeldGeneration_Should_ReleaseItsReservation()
    {
        await using var context = CreateDbContext();
        var meter = new RecordingMeter();
        var reservationId = Guid.NewGuid();
        _band = SafetyBandNames.Under6;
        var gate = CreateGate(context, meter, new StubClassifier());

        await gate.ScreenOutputAsync(ARequest(reservationId), AnOutput());

        // A hold can last two weeks and a reservation cannot, so keeping it would only mean the
        // platform's own sweeper expiring it later. Charging for a story a parent had to approve by
        // hand is a worse trade than the credit is worth.
        meter.Released.Should().ContainSingle();
    }

    [Fact]
    public async Task AnAllowedGeneration_Should_NotReleaseItsReservation()
    {
        await using var context = CreateDbContext();
        var meter = new RecordingMeter();
        var gate = CreateGate(context, meter, new StubClassifier());

        await gate.ScreenOutputAsync(ARequest(Guid.NewGuid()), AnOutput());

        // The gate does not commit either — only the caller knows the actual quantity. Releasing a
        // delivered generation would make every story free.
        meter.Released.Should().BeEmpty();
    }

    [Fact]
    public async Task AFailedRelease_Should_NotTurnARefusalIntoADelivery()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(
            context,
            new RecordingMeter(new InvalidOperationException("billing down")),
            new StubClassifier(new Dictionary<string, double> { [SafetyCategories.Sexual] = 0.99 }));

        var verdict = await gate.ScreenOutputAsync(ARequest(Guid.NewGuid()), AnOutput());

        // Over-charging by one credit is a support conversation. The alternative is not.
        verdict.Allowed.Should().BeFalse();
        verdict.Permit.Should().BeNull();
    }

    [Fact]
    public async Task NoMeterConfigured_Should_NotBreakTheGate()
    {
        await using var context = CreateDbContext();
        var gate = CreateGate(context, meter: null, new StubClassifier(
            new Dictionary<string, double> { [SafetyCategories.Sexual] = 0.99 }));

        // Aonik.Ai must not require Subscriptions to be registered in order to keep children safe.
        var verdict = await gate.ScreenOutputAsync(ARequest(Guid.NewGuid()), AnOutput());

        verdict.Outcome.Should().Be(SafetyDecisionOutcome.Blocked);
    }

    [Fact]
    public async Task NoReservation_Should_NotCallTheMeter()
    {
        await using var context = CreateDbContext();
        var meter = new RecordingMeter();
        var gate = CreateGate(context, meter, new StubClassifier(
            new Dictionary<string, double> { [SafetyCategories.Sexual] = 0.99 }));

        await gate.ScreenOutputAsync(
            new SafetyRequest(Guid.NewGuid(), SafetyModalities.Text, Guid.NewGuid()),
            AnOutput());

        meter.Released.Should().BeEmpty();
    }

    // ── Safety is identical on every tier (§18.28) ───────────────────────

    [Fact]
    public void TheGate_Should_NotDependOnAnEntitlementReader()
    {
        var dependencies = typeof(ContentSafetyGate)
            .GetConstructors()[0]
            .GetParameters()
            .Select(p => p.ParameterType.Name)
            .ToList();

        // Safety behaviour is identical on the £0 tier and every paid tier, and the way to guarantee
        // that is for the gate to have no way to find out which tier it is on. IUsageMeter is present
        // only to give a credit BACK, never to decide anything.
        dependencies.Should().NotContain(nameof(IEntitlementReader));
        dependencies.Should().Contain(nameof(IUsageMeter));
    }

    // ── What the child reads (§18.7) ─────────────────────────────────────

    [Theory]
    [InlineData(SafetyBandNames.Under6)]
    [InlineData(SafetyBandNames.Age6To9)]
    [InlineData(SafetyBandNames.Age10To12)]
    [InlineData(SafetyBandNames.Age13ToMajority)]
    public void BlockedAndUnavailable_Should_ReadIdenticallyToTheChild(string band)
    {
        var blocked = ChildFacingMessages.For(SafetyDecisionOutcome.Blocked, band);
        var unavailable = ChildFacingMessages.For(SafetyDecisionOutcome.CheckUnavailable, band);

        // Deliberate twice over: a failure to check is not a child's problem to understand, and a
        // distinguishable outage message is a probe — try until the wording changes, then send the
        // thing you wanted through.
        unavailable.Should().Be(blocked);
        blocked.CanRetry.Should().BeTrue();
    }

    [Theory]
    [InlineData(SafetyDecisionOutcome.Blocked)]
    [InlineData(SafetyDecisionOutcome.CheckUnavailable)]
    [InlineData(SafetyDecisionOutcome.HeldForReview)]
    [InlineData(SafetyDecisionOutcome.ModalityDisabled)]
    public void NoMessage_Should_NameACategoryOrSoundLikeAnAccusation(SafetyDecisionOutcome outcome)
    {
        foreach (var band in SafetyBandNames.All)
        {
            var message = ChildFacingMessages.For(outcome, band);

            foreach (var category in SafetyCategories.All)
            {
                message.Text.Should().NotContainEquivalentOf(category);
            }

            foreach (var word in new[]
                { "filter", "blocked", "unsafe", "violat", "not allowed", "rule", "warning", "inappropriate" })
            {
                message.Text.Should().NotContainEquivalentOf(word,
                    "a seven-year-old told they broke a rule learns they did something wrong; they did not");
            }
        }
    }

    [Fact]
    public void AHeldMessage_Should_TellTheChildAnAdultIsLooking_AndNotOfferRetry()
    {
        var message = ChildFacingMessages.For(SafetyDecisionOutcome.HeldForReview, SafetyBandNames.Under6);

        message.Text.Should().ContainEquivalentOf("grown-up");
        message.CanRetry.Should().BeFalse(
            "telling a child to retry something an adult is already reviewing produces a second copy "
            + "and no explanation");
    }

    [Fact]
    public void AnUnknownOutcome_Should_FallBackToTheSafeWording()
    {
        // A new outcome must not be able to leak a technical string to a child because someone forgot
        // a switch arm.
        var message = ChildFacingMessages.For((SafetyDecisionOutcome)99, SafetyBandNames.Age6To9);

        message.Text.Should().Be(
            ChildFacingMessages.For(SafetyDecisionOutcome.Blocked, SafetyBandNames.Age6To9).Text);
    }

    [Fact]
    public void AnAllowedOutcome_Should_HaveNoMessage()
    {
        ChildFacingMessages.For(SafetyDecisionOutcome.Allowed, SafetyBandNames.Age6To9).Text
            .Should().BeEmpty("there is nothing to explain when it worked");
    }
}
