using Aonik.Ai.Persistence;
using Aonik.Ai.Services.Safety;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Safety;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.TestSupport.Multitenancy;

using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Ai;

/// <summary>
/// Builds a gate whose pre-review lookup fails, so the ordering introduced by the S6/round-3 change
/// can be tested.
///
/// <para>
/// Pre-review is resolved <em>before</em> the decision is recorded, which is what makes the decision
/// truthful at write time. The cost is that a failure there sits between a completed classifier run
/// and the record that would explain it — so this harness exists to keep that path honest.
/// </para>
/// </summary>
internal static class ThrowingPreReviewHarness
{
    internal sealed class RecordingMeter : IUsageMeter
    {
        public List<Guid> Released { get; } = [];

        public Task ReleaseAsync(Guid reservationId, CancellationToken cancellationToken = default)
        {
            Released.Add(reservationId);
            return Task.CompletedTask;
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
            SubscriberRef subscriber, string meterCode, string holderRef, long weight = 1,
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

    private sealed class ThrowingPreReview : IGuardianPreReviewService
    {
        public Task<bool> RequiresPreReviewAsync(
            Guid subjectPartyId, string safetyBand, CancellationToken cancellationToken = default)
            => Task.FromException<bool>(new InvalidOperationException("database unavailable"));

        public Task<IReadOnlyList<PendingReviewItem>> ListPendingAsync(
            Guid guardianPartyId, Guid childPartyId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PreReviewDecision> ApproveAsync(
            Guid guardianPartyId, Guid pendingReviewId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PreReviewDecision> DeclineAsync(
            Guid guardianPartyId, Guid pendingReviewId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task SetPreReviewAsync(
            Guid guardianPartyId, Guid childPartyId, bool enabled,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SupervisionDisclosure> DescribeSupervisionAsync(
            Guid childPartyId, string safetyBand, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class CleanClassifier : IContentClassifier
    {
        public string Modality => SafetyModalities.Text;

        public Task<ClassificationResult> ClassifyAsync(
            ClassificationRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ClassificationResult(new Dictionary<string, double>(), Guid.NewGuid()));
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow { get; } = new(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);
    }

    public static ContentSafetyGate CreateGate(AiDbContext context, Guid tenantId, RecordingMeter meter)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new SafetyOptions());

        return new ContentSafetyGate(
            context,
            new SafetyPolicyReader(context, new TestTenantProvider(tenantId)),
            [new CleanClassifier()],
            new SafetyIncidentRecorder(context, options, NullLogger<SafetyIncidentRecorder>.Instance),
            new ThrowingPreReview(),
            new StubSafetyBandReader(SafetyBandNames.Age10To12),
            preservedInputStore: null,
            meter,
            new TestTenantProvider(tenantId),
            new FixedClock(),
            options,
            NullLogger<ContentSafetyGate>.Instance);
    }
}
